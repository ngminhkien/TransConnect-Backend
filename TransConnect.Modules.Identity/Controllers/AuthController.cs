using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Response;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Dto.Responses;
using TransConnect.Modules.Identity.Services;

namespace TransConnect.Modules.Identity.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CurrentUserResponse>>> Me(CancellationToken cancellationToken)
    {
        var result = await authService.GetCurrentUserAsync(cancellationToken);
        return Ok(ApiResponse<CurrentUserResponse>.Success(result));
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register(
        RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Success(result));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login(
        LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Success(result));
    }

    [HttpPost("google")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> GoogleLogin(
        GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.GoogleLoginAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Success(result));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh(
        RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request, cancellationToken);
        return Ok(ApiResponse<AuthResponse>.Success(result));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Logout(
        RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return Ok(ApiResponse<object>.Success(new { }, "Đăng xuất thành công."));
    }

    // Luôn trả về thành công, kể cả khi email không tồn tại — tránh biến endpoint này
    // thành công cụ dò xem email nào đã đăng ký.
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(
        ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await authService.ForgotPasswordAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Success(
            new { },
            "Nếu email tồn tại trong hệ thống, mã đặt lại mật khẩu đã được gửi."));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(
        ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await authService.ResetPasswordAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Success(
            new { },
            "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại trên tất cả thiết bị."));
    }

    [HttpPost("verify-email")]
    public async Task<ActionResult<ApiResponse<object>>> VerifyEmail(
        VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        await authService.VerifyEmailAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Success(new { }, "Xác thực email thành công."));
    }

    [HttpPost("resend-verification")]
    public async Task<ActionResult<ApiResponse<object>>> ResendVerification(
        ResendVerificationRequest request, CancellationToken cancellationToken)
    {
        await authService.ResendVerificationAsync(request, cancellationToken);
        return Ok(ApiResponse<object>.Success(
            new { },
            "Nếu email cần xác thực, mã mới đã được gửi."));
    }
}
