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
using TransConnect.Modules.Identity.Enums;
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
            AccountGroup = user.AccountGroup,
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
        if (request.AccountGroup is not (AccountGroups.Customer or AccountGroups.Operator))
            throw new ValidationAppException("Nhóm đăng ký không hợp lệ.");
        var emailTaken = await dbContext.Users.AnyAsync(u => u.Email == request.Email.Trim().ToLower() && u.AccountGroup == request.AccountGroup, cancellationToken);
        if (emailTaken)
        {
            throw new ConflictException($"Email '{request.Email}' đã được sử dụng.");
        }

        var user = new User
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            AccountGroup = request.AccountGroup,
            PhoneNumber = request.PhoneNumber,
            FullName = request.FullName,
            PasswordHash = passwordHasher.Hash(request.Password),
            IsActive = true
        };

        dbContext.Users.Add(user);
        await IdentityUniqueWrite.SaveAsync(dbContext,
            ct => dbContext.Users.IgnoreQueryFilters().AnyAsync(u => u.AccountGroup == user.AccountGroup && u.Email == user.Email, ct),
            "Email đã được sử dụng trong nhóm tài khoản này.", cancellationToken);

        await AssignDefaultRoleAsync(user, cancellationToken);

        // Gửi mã xác thực nhưng KHÔNG chặn đăng ký: email hỏng thì tài khoản vẫn tạo xong,
        // người dùng xin gửi lại sau được.
        await TrySendVerificationCodeAsync(user, cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLower() && u.AccountGroup == request.AccountGroup, cancellationToken);

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

        if (request.AccountGroup == AccountGroups.InternalAdmin)
            throw new UnauthorizedAppException("Tài khoản nội bộ không đăng nhập bằng Google.");

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

        if (!payload.EmailVerified)
            throw new UnauthorizedAppException("Email Google chưa được xác thực.");

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.GoogleId == payload.Subject && u.AccountGroup == request.AccountGroup, cancellationToken);

        if (user is null)
        {
            user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == payload.Email.ToLower() && u.AccountGroup == request.AccountGroup, cancellationToken);

            if (user is null)
            {
                user = new User
                {
                    Email = payload.Email.Trim().ToLowerInvariant(),
                    AccountGroup = request.AccountGroup,
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
                if (!user.IsActive || (user.GoogleId is not null && user.GoogleId != payload.Subject))
                    throw new UnauthorizedAppException("Không thể liên kết tài khoản Google này.");
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
        var tokenHash = RefreshTokenHash.Compute(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens.AsNoTracking()
            .FirstOrDefaultAsync(rt => rt.Token == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.IsRevoked || storedToken.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedAppException("Refresh token không hợp lệ hoặc đã hết hạn.");
        }

        // Claim and successor insertion share one transaction. A conditional UPDATE makes
        // concurrent requests serialize on the database row, including across app instances.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (await IdentityUserLock.AcquireAsync(dbContext, storedToken.UserId, cancellationToken) != 1)
            throw new UnauthorizedAppException("Tài khoản không còn khả dụng. Vui lòng đăng nhập lại.");
        var now = DateTime.UtcNow;
        var claimed = await dbContext.RefreshTokens
            .Where(rt => rt.Id == storedToken.Id && !rt.IsUsed && !rt.IsRevoked && rt.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(rt => rt.IsUsed, true)
                .SetProperty(rt => rt.UpdatedAt, now), cancellationToken);

        if (claimed == 0)
        {
            var wasUsed = await dbContext.RefreshTokens.AnyAsync(
                rt => rt.Id == storedToken.Id && rt.IsUsed, cancellationToken);
            if (wasUsed)
            {
                logger.LogWarning("Phát hiện refresh token bị dùng lại cho UserId {UserId}.", storedToken.UserId);
                await RevokeAllTokensAsync(storedToken.UserId, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            throw new UnauthorizedAppException("Phát hiện refresh token bị dùng lại, vui lòng đăng nhập lại.");
        }

        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == storedToken.UserId && u.IsActive, cancellationToken)
            ?? throw new UnauthorizedAppException("Tài khoản không còn khả dụng. Vui lòng đăng nhập lại.");

        var response = await IssueTokensAsync(user, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } accountId)
            throw new UnauthorizedAppException("Vui lòng đăng nhập.");
        var tokenHash = RefreshTokenHash.Compute(refreshToken);
        var now = DateTime.UtcNow;
        await dbContext.RefreshTokens.Where(rt => rt.Token == tokenHash && rt.UserId == accountId && !rt.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(rt => rt.IsRevoked, true)
                .SetProperty(rt => rt.UpdatedAt, now), cancellationToken);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLower() && u.AccountGroup == request.AccountGroup, cancellationToken);

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
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLower() && u.AccountGroup == request.AccountGroup, cancellationToken);

        if (user is null || !user.IsActive)
        {
            // Cùng thông báo với mã sai, để không lộ email nào tồn tại.
            throw new UnauthorizedAppException("Mã xác thực không đúng hoặc đã hết hạn.");
        }

        await verificationCodeService.ConsumeAndExecuteAsync(
            user.Id, VerificationPurpose.PasswordReset, request.Code, async ct =>
            {
                user.PasswordHash = passwordHasher.Hash(request.NewPassword);
                user.EmailVerifiedAt ??= DateTime.UtcNow;
                await dbContext.SaveChangesAsync(ct);
                await RevokeAllTokensAsync(user.Id, ct);
            }, cancellationToken);

        logger.LogInformation("UserId {UserId} đã đặt lại mật khẩu, toàn bộ phiên cũ bị thu hồi.", user.Id);
    }

    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLower() && u.AccountGroup == request.AccountGroup, cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAppException("Mã xác thực không đúng hoặc đã hết hạn.");
        }

        if (user.EmailVerifiedAt is not null)
        {
            return;
        }

        await verificationCodeService.ConsumeAndExecuteAsync(
            user.Id, VerificationPurpose.EmailVerification, request.Code, async ct =>
            {
                user.EmailVerifiedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(ct);
            }, cancellationToken);

        logger.LogInformation("UserId {UserId} đã xác thực email.", user.Id);
    }

    public async Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == request.Email.Trim().ToLower() && u.AccountGroup == request.AccountGroup, cancellationToken);

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
        var now = DateTime.UtcNow;
        await dbContext.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ExecuteUpdateAsync(setters => setters.SetProperty(rt => rt.IsRevoked, true)
                .SetProperty(rt => rt.UpdatedAt, now), cancellationToken);
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
            Token = RefreshTokenHash.Compute(refreshToken),
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
            AccountGroup = user.AccountGroup,
            Email = user.Email,
            FullName = user.FullName,
            EmailVerified = user.EmailVerifiedAt is not null,
            Roles = roles
        };
    }
}
