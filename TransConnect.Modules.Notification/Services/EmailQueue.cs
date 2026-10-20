using Microsoft.Extensions.Logging;
using TransConnect.Common.Notifications;
using TransConnect.Modules.Notification.Entities;
using TransConnect.Modules.Notification.Enums;
using TransConnect.Modules.Notification.Persistence;
using TransConnect.Modules.Notification.Templates;

namespace TransConnect.Modules.Notification.Services;

public class EmailQueue(
    NotificationDbContext dbContext,
    ITemplateRenderer templateRenderer,
    ILogger<EmailQueue> logger) : IEmailQueue
{
    public async Task<Guid> EnqueueAsync(EmailRequest request, CancellationToken cancellationToken = default)
    {
        // Render ngay lúc enqueue chứ không để worker render: nội dung lưu lại đúng bằng thứ
        // người dùng nhận, và lỗi template lộ ra ngay tại chỗ gọi thay vì âm thầm trong worker.
        var rendered = templateRenderer.Render(request.TemplateCode, request.Placeholders);

        var message = new EmailMessage
        {
            ToEmail = request.ToEmail,
            ToName = request.ToName,
            Subject = rendered.Subject,
            BodyHtml = rendered.BodyHtml,
            BodyText = rendered.BodyText,
            TemplateCode = request.TemplateCode,
            SourceType = request.SourceType, SourceId = request.SourceId, DeliverBefore = request.DeliverBefore,
            RetentionCategory = request.TemplateCode switch
            {
                "password-reset" or "email-verification" => "Otp",
                "organization-invitation" => "Invitation", _ => "General"
            },
            Status = NotificationStatus.Pending,
            ScheduledAt = request.ScheduledAt ?? DateTime.UtcNow
        };

        dbContext.EmailMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Đã xếp hàng email {TemplateCode} tới {ToEmail} (Id {MessageId}).",
            request.TemplateCode, request.ToEmail, message.Id);

        return message.Id;
    }
}
