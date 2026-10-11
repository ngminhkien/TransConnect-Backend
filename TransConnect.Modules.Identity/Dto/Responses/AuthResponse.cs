namespace TransConnect.Modules.Identity.Dto.Responses;

public record AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; init; }
    public string RefreshToken { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;

    // Client dùng cờ này để nhắc người dùng xác thực email — không phải để chặn đăng nhập.
    public bool EmailVerified { get; init; }

    public IReadOnlyList<string> Roles { get; init; } = [];
}
