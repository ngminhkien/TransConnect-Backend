using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransConnect.Common.Maintenance;
using TransConnect.Common.Notifications;
using TransConnect.Modules.Notification.Enums;
using TransConnect.Modules.Notification.Persistence;

namespace TransConnect.Modules.Notification.Services;

public sealed class EmailRetentionTask(NotificationDbContext db, IEnumerable<IEmailDeliveryGuard> guards,
    IOptions<MaintenanceOptions> options, TimeProvider time, EmailFileCleanup files) : IMaintenanceTask
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var batchSize = options.Value.BatchSize;
        // Includes future scheduled/retry messages: consumed/revoked sources should lose their body too.
        var pending = await db.EmailMessages.IgnoreQueryFilters().AsNoTracking().Where(x => x.Status == NotificationStatus.Pending)
            .OrderBy(x => x.EligibilityCheckedAt).ThenBy(x => x.CreatedAt).Take(batchSize).ToListAsync(ct);
        foreach (var message in pending)
        {
            ct.ThrowIfCancellationRequested();
            var valid = message.DeletedAt == null && !(message.DeliverBefore <= now)
                && !(message.RetentionCategory == "General" && message.CreatedAt.AddHours(24) <= now);
            if (valid && message.RetentionCategory != "General")
            {
                var guard = guards.SingleOrDefault(x => x.SourceType == message.SourceType);
                valid = message.SourceId is { } id && guard is not null
                    && await guard.CanDeliverAsync(id, message.ToEmail, ct);
            }
            var row = db.EmailMessages.IgnoreQueryFilters().Where(x => x.Id == message.Id && x.Status == NotificationStatus.Pending);
            if (!valid)
                await row.ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, NotificationStatus.Failed)
                    .SetProperty(x => x.TerminalAt, now).SetProperty(x => x.BodyPurgedAt, now)
                    .SetProperty(x => x.BodyHtml, "").SetProperty(x => x.BodyText, "")
                    .SetProperty(x => x.LastError, "SourceInactiveOrExpired")
                    .SetProperty(x => x.NextAttemptAt, (DateTime?)null), ct);
            else await row.ExecuteUpdateAsync(s => s.SetProperty(x => x.EligibilityCheckedAt, now), ct);
        }

        var legacyIds = await db.EmailMessages.IgnoreQueryFilters().Where(x => (x.Status == NotificationStatus.Sent || x.Status == NotificationStatus.Failed)
            && (x.TerminalAt == null || x.BodyPurgedAt == null || x.BodyHtml != "" || x.BodyText != ""))
            .OrderBy(x => x.CreatedAt).Take(batchSize).Select(x => x.Id).ToListAsync(ct);
        await db.EmailMessages.IgnoreQueryFilters().Where(x => legacyIds.Contains(x.Id) && (x.Status == NotificationStatus.Sent || x.Status == NotificationStatus.Failed))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.BodyHtml, "").SetProperty(x => x.BodyText, "")
                .SetProperty(x => x.TerminalAt, x => x.TerminalAt ?? x.SentAt ?? now)
                .SetProperty(x => x.BodyPurgedAt, x => x.BodyPurgedAt ?? now).SetProperty(x => x.LastError, (string?)null), ct);

        var otpCutoff = now.AddDays(-7);
        var otherCutoff = now.AddDays(-30);
        var ids = await db.EmailMessages.IgnoreQueryFilters().Where(x => (x.Status == NotificationStatus.Sent || x.Status == NotificationStatus.Failed)
            && x.TerminalAt != null && ((x.RetentionCategory == "Otp" && x.TerminalAt < otpCutoff)
                || (x.RetentionCategory != "Otp" && x.TerminalAt < otherCutoff)))
            .OrderBy(x => x.TerminalAt).Take(batchSize).Select(x => x.Id).ToListAsync(ct);
        await db.EmailMessages.IgnoreQueryFilters().Where(x => ids.Contains(x.Id) && (x.Status == NotificationStatus.Sent || x.Status == NotificationStatus.Failed))
            .ExecuteDeleteAsync(ct);
        await files.DeleteLegacyCopiesAsync(ct);
    }
}
