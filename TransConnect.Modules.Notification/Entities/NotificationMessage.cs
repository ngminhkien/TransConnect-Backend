using TransConnect.Base.Entities;
using TransConnect.Modules.Notification.Enums;

namespace TransConnect.Modules.Notification.Entities;

// Dữ liệu vòng đời gửi dùng chung cho các kênh thông báo.
// Địa chỉ nhận và nội dung đặc thù thuộc entity của từng kênh.
public abstract class NotificationMessage : BaseEntity
{
    // Giữ lại để thống kê / truy vết, không dùng để render lại khi gửi.
    public string TemplateCode { get; set; } = string.Empty;

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    // Thời điểm sớm nhất được phép gửi. Gửi ngay = thời điểm enqueue.
    public DateTime ScheduledAt { get; set; }

    public int AttemptCount { get; set; }

    // Lần thử kế tiếp sau khi gửi hụt. null = chưa có lịch retry.
    public DateTime? NextAttemptAt { get; set; }

    public DateTime? SentAt { get; set; }

    // Thời điểm worker nhận thông báo, dùng để phát hiện lượt gửi kẹt.
    public DateTime? ProcessingStartedAt { get; set; }

    public string? LastError { get; set; }
}
