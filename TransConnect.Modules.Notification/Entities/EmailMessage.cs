namespace TransConnect.Modules.Notification.Entities;

// Bảng outbox: nơi email nằm chờ giữa lúc nghiệp vụ yêu cầu gửi và lúc worker gửi thật.
// Đồng thời là nhật ký gửi — giữ nguyên nội dung đã gửi để về sau còn truy vết được.
public class EmailMessage : NotificationMessage
{
    public string ToEmail { get; set; } = string.Empty;
    public string? ToName { get; set; }

    // Nội dung được render NGAY LÚC enqueue (không render lúc gửi). Nhờ vậy sửa template
    // không làm đổi nội dung email đang nằm chờ, và log lưu đúng thứ user thực sự nhận.
    public string Subject { get; set; } = string.Empty;
    public string BodyHtml { get; set; } = string.Empty;
    public string BodyText { get; set; } = string.Empty;
}
