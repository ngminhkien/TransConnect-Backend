namespace TransConnect.Modules.Notification.Enums;

public enum NotificationStatus
{
    // Đang chờ gửi, kể cả thông báo đang chờ retry.
    Pending = 0,

    // Worker đã nhận và đang gửi; không bảo đảm chống gửi trùng giữa nhiều instance.
    Sending = 1,

    Sent = 2,

    // Đã thử đủ MaxAttempts mà vẫn lỗi — không tự thử lại nữa, cần can thiệp tay.
    Failed = 3
}
