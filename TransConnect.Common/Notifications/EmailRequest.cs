namespace TransConnect.Common.Notifications;

// Yêu cầu gửi 1 email. Bên gọi (module nghiệp vụ) chỉ mô tả "gửi template nào, cho ai, với
// dữ liệu gì" — không dựng HTML, không biết gửi bằng SMTP hay dịch vụ nào.
public record EmailRequest
{
    public required string ToEmail { get; init; }

    public string? ToName { get; init; }

    // Xem EmailTemplateCodes. Template thực tế nằm trong module Notification.
    public required string TemplateCode { get; init; }

    // Giá trị thay vào placeholder {{Key}} của template. Key không phân biệt hoa thường.
    public IReadOnlyDictionary<string, string> Placeholders { get; init; } =
        new Dictionary<string, string>();

    // null = gửi ngay. Để sẵn cho việc gửi theo lịch sau này mà không phải đổi contract.
    public DateTime? ScheduledAt { get; init; }
    public string? SourceType { get; init; }
    public Guid? SourceId { get; init; }
    public DateTime? DeliverBefore { get; init; }
}
