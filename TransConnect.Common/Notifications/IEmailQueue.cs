namespace TransConnect.Common.Notifications;

// Contract đặt ở Common để module nghiệp vụ (Identity, Wallet...) gọi được mà KHÔNG cần
// reference module Notification — xem quy tắc trong docs/base-libraries-guidelines.md.
public interface IEmailQueue
{
    // Chỉ ghi email vào hàng đợi rồi trả về ngay; việc gửi thật do background worker của
    // module Notification làm. Nhờ vậy request không phải chờ SMTP và gửi lỗi thì retry được.
    //
    // LƯU Ý: hàng đợi nằm ở DbContext của module Notification, KHÔNG chung transaction với
    // DbContext của module gọi. Vì vậy hãy gọi Enqueue SAU khi nghiệp vụ đã lưu thành công —
    // lỡ enqueue hỏng thì chỉ mất email (user xin gửi lại được), còn enqueue trước mà nghiệp
    // vụ rollback thì email báo thành công cho một giao dịch không tồn tại, tệ hơn nhiều.
    Task<Guid> EnqueueAsync(EmailRequest request, CancellationToken cancellationToken = default);
}
