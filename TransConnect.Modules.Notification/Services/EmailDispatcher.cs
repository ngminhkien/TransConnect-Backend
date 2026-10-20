using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransConnect.Common.Notifications;
using TransConnect.Modules.Notification.Entities;
using TransConnect.Modules.Notification.Enums;
using TransConnect.Modules.Notification.Options;
using TransConnect.Modules.Notification.Persistence;
using TransConnect.Modules.Notification.Providers;

namespace TransConnect.Modules.Notification.Services;

public sealed class EmailDispatcher(NotificationDbContext db, IEmailProvider provider,
    IEnumerable<IEmailDeliveryGuard> guards, IOptions<EmailOptions> options, TimeProvider time,
    ILogger<EmailDispatcher> logger)
{
    public async Task ProcessBatchAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var stuckBefore = now.AddMinutes(-options.Value.Worker.StuckAfterMinutes);
        await db.EmailMessages.Where(x => x.Status == NotificationStatus.Sending && x.ProcessingStartedAt < stuckBefore)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, NotificationStatus.Pending)
                .SetProperty(x => x.DispatchLease, (Guid?)null).SetProperty(x => x.ProcessingStartedAt, (DateTime?)null), ct);
        var ids = await db.EmailMessages.AsNoTracking().Where(x => x.Status == NotificationStatus.Pending
                && x.ScheduledAt <= now && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
            .OrderBy(x => x.ScheduledAt).Take(options.Value.Worker.BatchSize).Select(x => x.Id).ToListAsync(ct);
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            var lease = Guid.NewGuid();
            if (await db.EmailMessages.Where(x => x.Id == id && x.Status == NotificationStatus.Pending
                    && x.ScheduledAt <= now && (x.NextAttemptAt == null || x.NextAttemptAt <= now))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, NotificationStatus.Sending)
                    .SetProperty(x => x.DispatchLease, lease).SetProperty(x => x.ProcessingStartedAt, now), ct) != 1) continue;
            var message = await db.EmailMessages.AsNoTracking().SingleAsync(x => x.Id == id, ct);
            try
            {
                if (!await CanSendAsync(message, ct))
                {
                    await FinishAsync(message, lease, false, "SourceInactiveOrExpired", ct);
                    continue;
                }
                await db.EmailMessages.Where(x => x.Id == id && x.DispatchLease == lease)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1), ct);
                await provider.SendAsync(new OutgoingEmail(message.ToEmail, message.ToName, message.Subject,
                    message.BodyHtml, message.BodyText), ct);
                await FinishAsync(message, lease, true, null, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Release without consuming the body. A later pass rechecks source validity.
                await Owned(id, lease).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, NotificationStatus.Pending)
                    .SetProperty(x => x.DispatchLease, (Guid?)null)
                    .SetProperty(x => x.ProcessingStartedAt, (DateTime?)null), CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                // Provider errors may embed a message, OTP or URL. Persist/log only the type.
                var errorCode = ex.GetType().Name;
                var attempts = await db.EmailMessages.Where(x => x.Id == id).Select(x => x.AttemptCount).SingleAsync(ct);
                var permanent = ex is MailKit.Net.Smtp.SmtpCommandException smtp && (int)smtp.StatusCode >= 500;
                if (attempts >= options.Value.Worker.MaxAttempts || permanent)
                    await FinishAsync(message, lease, false, errorCode, ct);
                else
                {
                    var delay = Math.Min(options.Value.Worker.BackoffMaxSeconds,
                        options.Value.Worker.BackoffBaseSeconds * Math.Pow(2, Math.Max(0, attempts - 1)));
                    var retryAt = time.GetUtcNow().UtcDateTime.AddSeconds(delay);
                    await Owned(id, lease).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, NotificationStatus.Pending)
                        .SetProperty(x => x.NextAttemptAt, retryAt).SetProperty(x => x.LastError, errorCode)
                        .SetProperty(x => x.DispatchLease, (Guid?)null)
                        .SetProperty(x => x.ProcessingStartedAt, (DateTime?)null), ct);
                }
                logger.LogWarning("Email {MessageId} dispatch failed with {ErrorCode}.", id, errorCode);
            }
        }
    }

    private IQueryable<EmailMessage> Owned(Guid id, Guid lease) => db.EmailMessages
        .Where(x => x.Id == id && x.Status == NotificationStatus.Sending && x.DispatchLease == lease);

    private async Task<bool> CanSendAsync(EmailMessage message, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        if (message.DeliverBefore <= now || (message.RetentionCategory == "General" && message.CreatedAt.AddHours(24) <= now)) return false;
        if (message.SourceType is null || message.SourceId is null)
            return message.RetentionCategory == "General";
        var guard = guards.SingleOrDefault(x => x.SourceType == message.SourceType);
        return guard is not null && await guard.CanDeliverAsync(message.SourceId.Value, message.ToEmail, ct);
    }

    private Task<int> FinishAsync(EmailMessage message, Guid lease, bool sent, string? error, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return Owned(message.Id, lease).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, sent ? NotificationStatus.Sent : NotificationStatus.Failed)
            .SetProperty(x => x.SentAt, sent ? now : (DateTime?)null)
            .SetProperty(x => x.TerminalAt, now).SetProperty(x => x.BodyPurgedAt, now)
            .SetProperty(x => x.BodyHtml, "").SetProperty(x => x.BodyText, "")
            .SetProperty(x => x.LastError, error).SetProperty(x => x.NextAttemptAt, (DateTime?)null)
            .SetProperty(x => x.DispatchLease, (Guid?)null).SetProperty(x => x.ProcessingStartedAt, (DateTime?)null), ct);
    }
}
