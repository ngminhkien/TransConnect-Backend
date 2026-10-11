namespace TransConnect.Modules.Notification.Entities;

public enum EmailStatus
{
    // Đang chờ tới lượt gửi (kể cả email đã thử gửi hụt và đang chờ retry).
    Pending = 0,

    // Worker đã nhận và đang gửi. Trạng thái này để 2 worker không cùng gửi 1 email.
    Sending = 1,

    Sent = 2,

    // Đã thử đủ MaxAttempts mà vẫn lỗi — không tự thử lại nữa, cần can thiệp tay.
    Failed = 3
}
