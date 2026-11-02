namespace TransConnect.Modules.Notification.Options;

public class EmailOptions
{
    public const string SectionName = "Email";

    // "File" = ghi ra đĩa thay vì gửi thật (dùng cho Development, tránh spam hộp thư thật
    // và đốt quota Gmail mỗi lần test). "Smtp" = gửi thật.
    public string Provider { get; set; } = EmailProviderNames.File;

    public EmailSenderOptions From { get; set; } = new();
    public SmtpOptions Smtp { get; set; } = new();
    public EmailWorkerOptions Worker { get; set; } = new();
    public EmailBrandingOptions Branding { get; set; } = new();

    // Chỉ dùng khi Provider = "File". Rỗng thì mặc định là <ContentRoot>/sent-emails.
    public string FileDropPath { get; set; } = string.Empty;
}

public static class EmailProviderNames
{
    public const string File = "File";
    public const string Smtp = "Smtp";
}

public class EmailSenderOptions
{
    public string Address { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class SmtpOptions
{
    // Plain/unauthenticated SMTP is restricted to loopback test mailboxes by startup validation.
    public bool Authenticate { get; set; } = true;
    public bool UseTls { get; set; } = true;
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string User { get; set; } = string.Empty;

    // Với Gmail đây là App Password (16 ký tự), KHÔNG phải mật khẩu tài khoản.
    public string Password { get; set; } = string.Empty;

    // Gmail cổng 587 dùng STARTTLS. Nếu đổi sang cổng 465 thì đặt false (SSL ngầm định).
    public bool UseStartTls { get; set; } = true;

    public bool IsLoopback => Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
        || (System.Net.IPAddress.TryParse(Host, out var ip) && System.Net.IPAddress.IsLoopback(ip));
}

public class EmailWorkerOptions
{
    public int PollSeconds { get; set; } = 15;
    public int BatchSize { get; set; } = 20;
    public int MaxAttempts { get; set; } = 5;

    // Chờ giữa 2 lần thử: BackoffBaseSeconds * 2^(AttemptCount-1), có trần.
    public int BackoffBaseSeconds { get; set; } = 30;
    public int BackoffMaxSeconds { get; set; } = 3600;

    // Email kẹt ở trạng thái Sending quá lâu (app crash giữa chừng) sẽ được trả về Pending.
    public int StuckAfterMinutes { get; set; } = 10;
}

// Toàn bộ phần nhận diện thương hiệu nằm ở đây, KHÔNG hard-code trong template.
// Mọi giá trị dưới đây dùng được trong template dưới dạng placeholder, vd {{AppName}}.
public class EmailBrandingOptions
{
    public string AppName { get; set; } = "TransConnect";
    public string PrimaryColor { get; set; } = "#0F62FE";
    public string LogoUrl { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyAddress { get; set; } = string.Empty;
    public string SupportEmail { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
}
