using TransConnect.Base.Entities;

namespace TransConnect.Modules.Identity.Entities;

// Hỗ trợ refresh-token rotation: JwtId liên kết với access token đã phát,
// IsUsed/IsRevoked để phát hiện tái sử dụng token (theo pattern AppGroupCar-Be/Logictic).
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // SHA-256 digest with sha256: prefix. Never persist the client bearer token.
    public string Token { get; set; } = string.Empty;
    public string JwtId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public bool IsRevoked { get; set; }
}
