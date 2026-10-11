using TransConnect.Base.Entities;

namespace TransConnect.Modules.Identity.Entities;

// Mã OTP dùng chung cho cả xác thực email lẫn đặt lại mật khẩu — phân biệt bằng Purpose.
// Gộp làm một vì hai luồng cần y hệt nhau: sinh mã, hết hạn, giới hạn số lần sai, dùng 1 lần.
public class VerificationCode : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public VerificationPurpose Purpose { get; set; }

    // Chỉ lưu hash, không lưu mã gốc — cùng lý do với mật khẩu: DB rò rỉ thì mã cũng
    // không dùng được ngay. Hash bằng BCrypt (chậm có chủ đích) chứ không phải SHA256,
    // vì mã 6 chữ số chỉ có 1 triệu khả năng, hash nhanh là dò hết trong vài giây.
    public string CodeHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    // Số lần nhập sai. Chạm ngưỡng thì mã bị vô hiệu hoá luôn, đây là thứ chặn dò mã.
    public int AttemptCount { get; set; }

    // true = không dùng được nữa (đã xác thực xong, hoặc bị thay bởi mã mới, hoặc nhập sai quá số lần).
    public bool IsUsed { get; set; }

    public DateTime? UsedAt { get; set; }
}
