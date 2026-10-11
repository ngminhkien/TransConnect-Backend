namespace TransConnect.Modules.Identity.Options;

public class VerificationOptions
{
    public const string SectionName = "Verification";

    public int CodeLength { get; set; } = 6;

    // Ngắn có chủ đích: mã 6 chữ số entropy thấp, sống càng lâu càng dễ bị dò.
    public int ExpiryMinutes { get; set; } = 10;

    // Nhập sai quá số lần này thì mã bị huỷ, phải xin mã mới.
    public int MaxAttempts { get; set; } = 5;

    // Chặn spam hộp thư người khác và chặn đốt hạn mức gửi của Gmail.
    public int MaxRequestsPerHour { get; set; } = 3;
}
