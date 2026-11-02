using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TransConnect.Modules.Notification.Options;

namespace TransConnect.Modules.Notification.Services;

public sealed class EmailFileCleanup(IOptions<EmailOptions> options, IHostEnvironment environment)
{
    public string DirectoryPath => Path.GetFullPath(string.IsNullOrWhiteSpace(options.Value.FileDropPath)
        ? Path.Combine(environment.ContentRootPath, "sent-emails") : options.Value.FileDropPath);

    public Task DeleteLegacyCopiesAsync(CancellationToken ct)
    {
        var directory = new DirectoryInfo(DirectoryPath);
        if (!directory.Exists || directory.Attributes.HasFlag(FileAttributes.ReparsePoint)) return Task.CompletedTask;
        foreach (var file in directory.EnumerateFiles("*.html", SearchOption.TopDirectoryOnly)
            .Where(f => Regex.IsMatch(f.Name, @"^(?:transconnect-email-[0-9a-f]{32}|\d{8}-\d{6}-\d{3}_.+)\.html$")).Take(200))
        {
            ct.ThrowIfCancellationRequested();
            if (!file.Attributes.HasFlag(FileAttributes.ReparsePoint)
                && Regex.IsMatch(file.Name, @"^(?:transconnect-email-[0-9a-f]{32}|\d{8}-\d{6}-\d{3}_.+)\.html$"))
                file.Delete();
        }
        return Task.CompletedTask;
    }
}
