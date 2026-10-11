namespace TransConnect.Common.Notifications;

// Mã template là 1 phần của contract giữa module gọi và module Notification, nên đặt ở Common.
// Giá trị phải khớp tên file trong TransConnect.Modules.Notification/Templates/.
public static class EmailTemplateCodes
{
    public const string EmailVerification = "email-verification";
    public const string PasswordReset = "password-reset";
    public const string TransactionNotice = "transaction-notice";
}
