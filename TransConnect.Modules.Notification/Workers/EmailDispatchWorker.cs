using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransConnect.Modules.Notification.Entities;
using TransConnect.Modules.Notification.Options;
using TransConnect.Modules.Notification.Persistence;
using TransConnect.Modules.Notification.Providers;

namespace TransConnect.Modules.Notification.Workers;

// Quét bảng outbox theo chu kỳ và gửi email đang chờ.
//
// BackgroundService là singleton, còn DbContext/IEmailProvider là scoped — nên KHÔNG được
// inject thẳng chúng vào constructor. Phải tạo scope riêng cho mỗi vòng lặp qua
// IServiceScopeFactory, nếu không DbContext sẽ sống suốt đời ứng dụng và phình bộ nhớ.
public class EmailDispatchWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOptions> emailOptions,
    ILogger<EmailDispatchWorker> logger) : BackgroundService
{
    private readonly EmailWorkerOptions _options = emailOptions.Value.Worker;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "EmailDispatchWorker khởi động (quét mỗi {PollSeconds}s, tối đa {BatchSize} email/lượt).",
            _options.PollSeconds, _options.BatchSize);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollSeconds));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ProcessBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Nuốt lỗi ở đây là CỐ Ý: một lượt quét hỏng (mất kết nối DB...) không được
                    // phép giết vòng lặp, nếu không toàn bộ email sau đó nằm chờ vĩnh viễn.
                    logger.LogError(ex, "Lỗi trong lượt quét email, sẽ thử lại ở lượt sau.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Ứng dụng đang tắt — thoát êm.
        }

        logger.LogInformation("EmailDispatchWorker đã dừng.");
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var provider = scope.ServiceProvider.GetRequiredService<IEmailProvider>();

        await ReclaimStuckMessagesAsync(dbContext, cancellationToken);

        var now = DateTime.UtcNow;

        var pending = await dbContext.EmailMessages
            .Where(e => e.Status == EmailStatus.Pending
                        && e.ScheduledAt <= now
                        && (e.NextAttemptAt == null || e.NextAttemptAt <= now))
            .OrderBy(e => e.ScheduledAt)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        // Đánh dấu cả lô là Sending trước khi gửi, để lượt quét sau không bốc lại đúng
        // những email này.
        foreach (var message in pending)
        {
            message.Status = EmailStatus.Sending;
            message.ProcessingStartedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var message in pending)
        {
            await SendOneAsync(dbContext, provider, message, cancellationToken);
        }
    }

    private async Task SendOneAsync(
        NotificationDbContext dbContext,
        IEmailProvider provider,
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        message.AttemptCount++;

        try
        {
            await provider.SendAsync(
                new OutgoingEmail(message.ToEmail, message.ToName, message.Subject, message.BodyHtml, message.BodyText),
                cancellationToken);

            message.Status = EmailStatus.Sent;
            message.SentAt = DateTime.UtcNow;
            message.NextAttemptAt = null;
            message.ProcessingStartedAt = null;
            message.LastError = null;
        }
        catch (Exception ex)
        {
            message.LastError = Truncate(ex.Message, 2000);
            message.ProcessingStartedAt = null;

            if (message.AttemptCount >= _options.MaxAttempts)
            {
                message.Status = EmailStatus.Failed;
                message.NextAttemptAt = null;

                logger.LogError(ex,
                    "Email {MessageId} tới {ToEmail} thất bại sau {AttemptCount} lần thử — dừng thử lại.",
                    message.Id, message.ToEmail, message.AttemptCount);
            }
            else
            {
                message.Status = EmailStatus.Pending;
                message.NextAttemptAt = DateTime.UtcNow.Add(GetBackoff(message.AttemptCount));

                logger.LogWarning(ex,
                    "Email {MessageId} tới {ToEmail} gửi hụt lần {AttemptCount}, thử lại lúc {NextAttemptAt}.",
                    message.Id, message.ToEmail, message.AttemptCount, message.NextAttemptAt);
            }
        }

        // Lưu từng email một: 1 email hỏng không kéo theo mất kết quả của những email đã gửi
        // thành công trong cùng lô.
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Ứng dụng crash giữa lúc gửi sẽ để lại row kẹt ở Sending mãi mãi. Trả chúng về Pending
    // sau một khoảng thời gian để được thử lại.
    private async Task ReclaimStuckMessagesAsync(
        NotificationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var threshold = DateTime.UtcNow.AddMinutes(-_options.StuckAfterMinutes);

        var stuck = await dbContext.EmailMessages
            .Where(e => e.Status == EmailStatus.Sending
                        && e.ProcessingStartedAt != null
                        && e.ProcessingStartedAt < threshold)
            .ToListAsync(cancellationToken);

        if (stuck.Count == 0)
        {
            return;
        }

        foreach (var message in stuck)
        {
            message.Status = EmailStatus.Pending;
            message.ProcessingStartedAt = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Đã thu hồi {Count} email kẹt ở trạng thái Sending.", stuck.Count);
    }

    private TimeSpan GetBackoff(int attemptCount)
    {
        // Giãn cách theo cấp số nhân: 30s, 60s, 120s, 240s... có trần để không chờ vô hạn.
        var seconds = _options.BackoffBaseSeconds * Math.Pow(2, attemptCount - 1);
        return TimeSpan.FromSeconds(Math.Min(seconds, _options.BackoffMaxSeconds));
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
