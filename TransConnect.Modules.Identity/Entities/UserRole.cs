namespace TransConnect.Modules.Identity.Entities;

// Bảng nối thuần túy, khóa phức hợp (UserId, RoleId) — không kế thừa BaseEntity vì
// không cần audit trail riêng cho quan hệ này.
public class UserRole
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = null!;
}
