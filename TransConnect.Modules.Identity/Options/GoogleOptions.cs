namespace TransConnect.Modules.Identity.Options;

// Chỉ Identity module cần Google config — không đưa vào TransConnect.Common
// (xem docs/base-libraries-guidelines.md: chỉ nâng cấp lên Common khi có module thứ 2 dùng lại).
public class GoogleOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;
}
