using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransConnect.Common.Notifications;
using TransConnect.Modules.Identity.Options;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Services;

public sealed class VerificationEmailGuard(IdentityDbContext db, IOptions<VerificationOptions> options,
    TimeProvider time) : IEmailDeliveryGuard
{
    public string SourceType => EmailSourceTypes.VerificationCode;
    public Task<bool> CanDeliverAsync(Guid sourceId, string recipient, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return db.VerificationCodes.AsNoTracking().AnyAsync(x => x.Id == sourceId && !x.IsUsed
            && x.ExpiresAt > now && x.AttemptCount < options.Value.MaxAttempts
            && x.User.IsActive && x.User.DeletedAt == null && x.User.Email == recipient, ct);
    }
}
