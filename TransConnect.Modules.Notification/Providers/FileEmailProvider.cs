using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransConnect.Modules.Notification.Options;

namespace TransConnect.Modules.Notification.Providers;

// Provider cho môi trường Development: ghi email ra file .html thay vì gửi thật.
// Mục đích: test luồng đăng ký / quên mật khẩu bao nhiêu lần cũng được mà không spam hộp
// thư thật và không đốt hạn mức gửi của Gmail. Mở file bằng trình duyệt là xem được.
public class FileEmailProvider(
    IOptions<EmailOptions> emailOptions,
    IHostEnvironment environment,
    ILogger<FileEmailProvider> logger) : IEmailProvider
{
    private readonly EmailOptions _options = emailOptions.Value;

    public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        var directory = string.IsNullOrWhiteSpace(_options.FileDropPath)
            ? Path.Combine(environment.ContentRootPath, "sent-emails")
            : _options.FileDropPath;

        Directory.CreateDirectory(directory);

        var fileName = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}_{Sanitize(email.ToEmail)}.html";
        var fullPath = Path.Combine(directory, fileName);

        // Chèn thêm 1 khối header hiển thị To/Subject để mở file ra là thấy đủ thông tin,
        // không phải mò trong log.
        var content = $"""
            <div style="font-family:monospace;background:#f4f4f4;padding:12px;margin-bottom:16px;border:1px solid #ccc">
              <div><b>To:</b> {email.ToName} &lt;{email.ToEmail}&gt;</div>
              <div><b>From:</b> {_options.From.DisplayName} &lt;{_options.From.Address}&gt;</div>
              <div><b>Subject:</b> {email.Subject}</div>
            </div>
            {email.BodyHtml}
            """;

        await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, cancellationToken);

        logger.LogInformation(
            "[FileEmailProvider] Email tới {ToEmail} đã được ghi ra {Path} (KHÔNG gửi thật).",
            email.ToEmail, fullPath);
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(c => invalid.Contains(c) ? '_' : c));
    }
}
