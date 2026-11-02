using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Dto.Responses;

namespace TransConnect.Modules.Identity.Services;

public interface IAuthService
{
    Task<CurrentUserResponse> UpdateProfileAsync(UpdateAccountProfileRequest request, CancellationToken cancellationToken = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default);
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    // Luôn kết thúc êm dù email có tồn tại hay không — xem ghi chú chống dò tài khoản
    // trong AuthService.
    Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default);

    Task ResendVerificationAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default);
}
