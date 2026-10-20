using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using TransConnect.Modules.Notification.Options;

namespace TransConnect.Modules.Notification.Providers;

// Dùng MailKit thay cho System.Net.Mail.SmtpClient: SmtpClient của BCL đã bị Microsoft
// khuyến cáo không dùng cho code mới và xử lý STARTTLS không đầy đủ.
public class SmtpEmailProvider(
    IOptions<EmailOptions> emailOptions,
    ILogger<SmtpEmailProvider> logger) : IEmailProvider
{
    private readonly EmailOptions _options = emailOptions.Value;

    public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.From.DisplayName, _options.From.Address));
        message.To.Add(new MailboxAddress(email.ToName ?? string.Empty, email.ToEmail));
        message.Subject = email.Subject;

        // Gửi kèm cả bản HTML lẫn plain-text (multipart/alternative): email chỉ có HTML
        // bị bộ lọc spam trừ điểm, và một số client vẫn hiển thị bản text.
        message.Body = new BodyBuilder
        {
            HtmlBody = email.BodyHtml,
            TextBody = email.BodyText
        }.ToMessageBody();

        using var client = new SmtpClient();

        var socketOptions = !_options.Smtp.UseTls ? SecureSocketOptions.None
            : _options.Smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.SslOnConnect;

        await client.ConnectAsync(_options.Smtp.Host, _options.Smtp.Port, socketOptions, cancellationToken);
        if (_options.Smtp.Authenticate)
            await client.AuthenticateAsync(_options.Smtp.User, _options.Smtp.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("Đã gửi email tới {ToEmail} qua SMTP {Host}.", email.ToEmail, _options.Smtp.Host);
    }
}
