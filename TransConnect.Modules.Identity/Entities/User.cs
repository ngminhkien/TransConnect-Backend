using TransConnect.Base.Entities;

namespace TransConnect.Modules.Identity.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    // Null với tài khoản chỉ đăng nhập bằng Google (không có mật khẩu nội bộ).
    public string? PasswordHash { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsActive { get; set; } = true;

    // null = chưa xác thực email. CỐ TÌNH không chặn đăng nhập khi chưa xác thực — chỉ dùng
    // để chặn các thao tác nhạy cảm về sau (rút tiền...). Xem docs/email-and-notification.md.
    public DateTime? EmailVerifiedAt { get; set; }

    // Google "sub" claim — null nếu tài khoản chưa liên kết Google.
    public string? GoogleId { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
