using TransConnect.Base.Entities;

namespace TransConnect.Modules.Notification.Entities;

// Bảng outbox: nơi email nằm chờ giữa lúc nghiệp vụ yêu cầu gửi và lúc worker gửi thật.
// Đồng thời là nhật ký gửi — giữ nguyên nội dung đã gửi để về sau còn truy vết được.
public class EmailMessage : BaseEntity
{
    public string ToEmail { get; set; } = string.Empty;
    public string? ToName { get; set; }

    // Nội dung được render NGAY LÚC enqueue (không render lúc gửi). Nhờ vậy sửa template
    // không làm đổi nội dung email đang nằm chờ, và log lưu đúng thứ user thực sự nhận.
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;

    // Giữ lại để thống kê / truy vết, không dùng để render lại.
    public string TemplateCode { get; set; } = string.Empty;

    public EmailStatus Status { get; set; } = EmailStatus.Pending;

    // Thời điểm sớm nhất được phép gửi. Gửi ngay = thời điểm enqueue.
    public DateTime ScheduledAt { get; set; }

    public int AttemptCount { get; set; }

    // Lần thử kế tiếp (backoff sau khi gửi hụt). null = chưa từng thất bại.
    public DateTime? NextAttemptAt { get; set; }

    public DateTime? SentAt { get; set; }

    // Thời điểm worker nhận email này, dùng để phát hiện row kẹt khi app crash giữa chừng.
    public DateTime? ProcessingStartedAt { get; set; }

    public string? LastError { get; set; }
}
