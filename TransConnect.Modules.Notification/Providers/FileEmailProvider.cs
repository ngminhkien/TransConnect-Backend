using System.Text;
using Microsoft.Extensions.Logging;
using TransConnect.Modules.Notification.Services;

namespace TransConnect.Modules.Notification.Providers;

// Development sink only. Use a test SMTP mailbox to read OTPs: no internal copy is retained.
public sealed class FileEmailProvider(EmailFileCleanup files, ILogger<FileEmailProvider> logger) : IEmailProvider
{
    public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default)
    {
        var directory = files.DirectoryPath;
        Directory.CreateDirectory(directory);
        if (new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidOperationException("Email file directory cannot be a symbolic link.");
        var fullPath = Path.Combine(directory, $"transconnect-email-{Guid.NewGuid():N}.html");
        try { await File.WriteAllTextAsync(fullPath, email.BodyHtml, Encoding.UTF8, cancellationToken); }
        finally { File.Delete(fullPath); }
        logger.LogInformation("Development email sink completed; internal copy removed. Use test SMTP for mailbox inspection.");
    }
}
