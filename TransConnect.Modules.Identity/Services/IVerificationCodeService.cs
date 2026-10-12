using TransConnect.Modules.Identity.Entities;

namespace TransConnect.Modules.Identity.Services;

public interface IVerificationCodeService
{
    // Sinh mã mới, lưu hash và trả về mã gốc để gửi qua email. Mã cũ cùng mục đích bị vô hiệu hoá.
    // Ném TooManyRequestsException nếu vượt quá số lần xin mã cho phép trong 1 giờ.
    Task<string> IssueAsync(
        Guid userId,
        VerificationPurpose purpose,
        CancellationToken cancellationToken = default);

    // Kiểm tra mã và tiêu huỷ nếu đúng. Ném UnauthorizedAppException nếu sai/hết hạn/quá số lần thử.
    Task ConsumeAsync(
        Guid userId,
        VerificationPurpose purpose,
        string code,
        CancellationToken cancellationToken = default);

    // Callback chỉ ghi IdentityDbContext cùng scope, không mở transaction khác hoặc gửi email.
    // Mã đúng và callback commit cùng nhau; mã sai vẫn commit bộ đếm trước khi ném lỗi.
    Task ConsumeAndExecuteAsync(
        Guid userId,
        VerificationPurpose purpose,
        string code,
        Func<CancellationToken, Task> onVerified,
        CancellationToken cancellationToken = default);

    int ExpiryMinutes { get; }
}
