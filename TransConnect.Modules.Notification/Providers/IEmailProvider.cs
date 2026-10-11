namespace TransConnect.Modules.Notification.Providers;

// Email đã render xong, sẵn sàng đẩy đi. Provider chỉ lo việc "đưa được nó ra ngoài".
public record OutgoingEmail(
    string ToEmail,
    string? ToName,
    string Subject,
    string BodyHtml,
    string BodyText);

// Ranh giới giữa "gửi cái gì" và "gửi bằng đường nào". Thêm nhà cung cấp mới (SendGrid,
// Amazon SES, Resend...) chỉ cần thêm 1 class implement interface này rồi đổi Email:Provider,
// không đụng tới worker hay bất kỳ module nghiệp vụ nào.
public interface IEmailProvider
{
    Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default);
}
