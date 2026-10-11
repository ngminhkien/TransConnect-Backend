using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Common.Notifications;
using TransConnect.Common.Options;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Dto.Responses;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Options;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Services;

public class AuthService(
    IdentityDbContext dbContext,
    ICurrentUser currentUser,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IPermissionService permissionService,
    IVerificationCodeService verificationCodeService,
    IEmailQueue emailQueue,
    IOptions<JwtOptions> jwtOptions,
    IOptions<GoogleOptions> googleOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private const string DefaultRoleName = "User";
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly GoogleOptions _googleOptions = googleOptions.Value;

    public async Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            throw new UnauthorizedAppException("Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.");
        }

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
            ?? throw new UnauthorizedAppException("Tài khoản không còn khả dụng. Vui lòng đăng nhập lại.");

        return new CurrentUserResponse
        {
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            EmailVerified = user.EmailVerifiedAt is not null,
            Roles = await permissionService.GetRoleNamesAsync(userId, cancellationToken),
            Permissions = await permissionService.GetPermissionCodesAsync(userId, cancellationToken)
        };
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var emailTaken = await dbContext.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailTaken)
        {
            throw new ConflictException($"Email '{request.Email}' đã được sử dụng.");
        }

        var user = new User
        {
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            FullName = request.FullName,
            PasswordHash = passwordHasher.Hash(request.Password),
            IsActive = true
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        await AssignDefaultRoleAsync(user, cancellationToken);

        // Gửi mã xác thực nhưng KHÔNG chặn đăng ký: email hỏng thì tài khoản vẫn tạo xong,
        // người dùng xin gửi lại sau được.
        await TrySendVerificationCodeAsync(user, cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null || !user.IsActive || user.PasswordHash is null
            || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAppException("Email hoặc mật khẩu không đúng.");
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default)
    {
        // Chặn ngay từ đầu khi chưa cấu hình, KHÔNG chạy tiếp ở chế độ bỏ qua kiểm tra `aud`.
        // Thiếu kiểm tra `aud` nghĩa là chấp nhận mọi ID token do Google ký — kể cả token phát
        // cho ứng dụng của kẻ tấn công — nên hắn chỉ cần dụ nạn nhân đăng nhập vào app của mình
        // là chiếm được tài khoản ở đây. Xem docs/auth-and-verification.md.
        if (string.IsNullOrWhiteSpace(_googleOptions.ClientId))
        {
            logger.LogError(
                "Chưa cấu hình Google:ClientId — từ chối đăng nhập Google thay vì bỏ qua kiểm tra audience.");
            throw new UnauthorizedAppException("Đăng nhập Google chưa được cấu hình trên máy chủ.");
        }

        GoogleJsonWebSignature.Payload payload;
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_googleOptions.ClientId]
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không xác minh được mã đăng nhập Google.");
            throw new UnauthorizedAppException("Mã đăng nhập Google không hợp lệ. Vui lòng đăng nhập lại.");
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject, cancellationToken);

        if (user is null)
        {
            user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == payload.Email, cancellationToken);

            if (user is null)
            {
                user = new User
                {
                    Email = payload.Email,
                    FullName = payload.Name,
                    GoogleId = payload.Subject,
                    IsActive = true,
                    // Google đã xác minh email này rồi, bắt người dùng xác minh lại là thừa.
                    EmailVerifiedAt = DateTime.UtcNow
                };

                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync(cancellationToken);

                await AssignDefaultRoleAsync(user, cancellationToken);
            }
            else
            {
                // Tài khoản đã đăng ký bằng email/password, giờ liên kết thêm đăng nhập Google.
                user.GoogleId = payload.Subject;
                user.EmailVerifiedAt ??= DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else if (user.EmailVerifiedAt is null)
        {
            user.EmailVerifiedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAppException("Tài khoản đã bị vô hiệu hoá.");
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

        if (storedToken is null || storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            throw new UnauthorizedAppException("Refresh token không hợp lệ hoặc đã hết hạn.");
        }

        if (storedToken.IsUsed)
        {
            // Token đã dùng rồi mà còn bị trình lên lần nữa — dấu hiệu bị đánh cắp.
            // Revoke toàn bộ refresh token còn sống của user để chặn kẻ tấn công.
            logger.LogWarning(
                "Phát hiện refresh token bị dùng lại cho UserId {UserId} — revoke toàn bộ token còn hiệu lực.",
                storedToken.UserId);
            await RevokeAllTokensAsync(storedToken.UserId, cancellationToken);
            throw new UnauthorizedAppException("Phát hiện refresh token bị dùng lại, vui lòng đăng nhập lại.");
        }

        storedToken.IsUsed = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == storedToken.UserId, cancellationToken)
            ?? throw new NotFoundException("Người dùng", storedToken.UserId);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, cancellationToken);

        if (storedToken is not null)
        {
            storedToken.IsRevoked = true;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        // KHÔNG báo lỗi khi email không tồn tại: nếu phản hồi khác nhau giữa "có" và "không có",
        // API này thành công cụ dò xem email nào đã đăng ký trong hệ thống.
        if (user is null || !user.IsActive)
        {
            logger.LogInformation(
                "Yêu cầu quên mật khẩu cho email không tồn tại hoặc đã khoá — bỏ qua, vẫn trả về thành công.");
            return;
        }

        try
        {
            var code = await verificationCodeService.IssueAsync(
                user.Id, VerificationPurpose.PasswordReset, cancellationToken);

            await SendCodeEmailAsync(user, code, EmailTemplateCodes.PasswordReset, cancellationToken);
        }
        catch (TooManyRequestsException)
        {
            // Nuốt lỗi ở đây là CỐ Ý: trả 429 sẽ tiết lộ email này có tồn tại, đúng thứ
            // mà việc luôn trả thành công ở trên đang cố che.
            logger.LogWarning("UserId {UserId} xin mã đặt lại mật khẩu quá nhiều lần.", user.Id);
        }
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null || !user.IsActive)
        {
            // Cùng thông báo với mã sai, để không lộ email nào tồn tại.
            throw new UnauthorizedAppException("Mã xác thực không đúng hoặc đã hết hạn.");
        }

        await verificationCodeService.ConsumeAsync(
            user.Id, VerificationPurpose.PasswordReset, request.Code, cancellationToken);

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);

        // Nhập đúng mã gửi tới email nghĩa là đã chứng minh sở hữu email — coi như đã xác thực.
        user.EmailVerifiedAt ??= DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        // Đổi mật khẩu mà không đá các phiên cũ ra thì gần như vô nghĩa: kẻ đã chiếm được
        // tài khoản vẫn giữ nguyên refresh token và tiếp tục dùng được.
        await RevokeAllTokensAsync(user.Id, cancellationToken);

        logger.LogInformation("UserId {UserId} đã đặt lại mật khẩu, toàn bộ phiên cũ bị thu hồi.", user.Id);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAppException("Mã xác thực không đúng hoặc đã hết hạn.");
        }

        if (user.EmailVerifiedAt is not null)
        {
            return;
        }

        await verificationCodeService.ConsumeAsync(
            user.Id, VerificationPurpose.EmailVerification, request.Code, cancellationToken);

        user.EmailVerifiedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("UserId {UserId} đã xác thực email.", user.Id);
    }

    public async Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        // Im lặng khi không tìm thấy hoặc đã xác thực rồi — cùng lý do chống dò tài khoản.
        if (user is null || !user.IsActive || user.EmailVerifiedAt is not null)
        {
            return;
        }

        try
        {
            var code = await verificationCodeService.IssueAsync(
                user.Id, VerificationPurpose.EmailVerification, cancellationToken);

            await SendCodeEmailAsync(user, code, EmailTemplateCodes.EmailVerification, cancellationToken);
        }
        catch (TooManyRequestsException)
        {
            logger.LogWarning("UserId {UserId} xin mã xác thực email quá nhiều lần.", user.Id);
        }
    }

    private async Task TrySendVerificationCodeAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            var code = await verificationCodeService.IssueAsync(
                user.Id, VerificationPurpose.EmailVerification, cancellationToken);

            await SendCodeEmailAsync(user, code, EmailTemplateCodes.EmailVerification, cancellationToken);
        }
        catch (Exception ex)
        {
            // Đăng ký đã thành công rồi, không được để lỗi gửi mã làm hỏng cả request.
            logger.LogError(ex,
                "Không gửi được mã xác thực cho UserId {UserId}. Người dùng có thể yêu cầu gửi lại.",
                user.Id);
        }
    }

    private Task SendCodeEmailAsync(
        User user,
        string code,
        string templateCode,
        CancellationToken cancellationToken) =>
        emailQueue.EnqueueAsync(new EmailRequest
        {
            ToEmail = user.Email,
            ToName = user.FullName,
            TemplateCode = templateCode,
            Placeholders = new Dictionary<string, string>
            {
                ["FullName"] = user.FullName,
                ["OtpCode"] = code,
                ["ExpiresMinutes"] = verificationCodeService.ExpiryMinutes.ToString()
            }
        }, cancellationToken);

    private async Task AssignDefaultRoleAsync(User user, CancellationToken cancellationToken)
    {
        var defaultRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.Name == DefaultRoleName, cancellationToken);
        if (defaultRole is not null)
        {
            dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = defaultRole.Id });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RevokeAllTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken cancellationToken)
    {
        var roles = await permissionService.GetRoleNamesAsync(user.Id, cancellationToken);
        var permissions = await permissionService.GetPermissionCodesAsync(user.Id, cancellationToken);

        var accessToken = tokenService.GenerateAccessToken(user, roles, permissions);
        var refreshToken = tokenService.GenerateRefreshToken();

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshToken,
            JwtId = accessToken.JwtId,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken.AccessToken,
            AccessTokenExpiresAt = accessToken.ExpiresAt,
            RefreshToken = refreshToken,
            UserId = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            EmailVerified = user.EmailVerifiedAt is not null,
            Roles = roles
        };
    }
}
