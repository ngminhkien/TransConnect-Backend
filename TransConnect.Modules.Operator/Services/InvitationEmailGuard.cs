using Microsoft.EntityFrameworkCore;
using TransConnect.Common.Notifications;
using TransConnect.Modules.Operator.Persistence;

namespace TransConnect.Modules.Operator.Services;

public sealed class InvitationEmailGuard(OperatorDbContext db, TimeProvider time) : IEmailDeliveryGuard
{
    public string SourceType => EmailSourceTypes.Invitation;
    public Task<bool> CanDeliverAsync(Guid sourceId, string recipient, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return db.Invitations.AsNoTracking().AnyAsync(x => x.Id == sourceId && x.Email == recipient
            && x.AcceptedAt == null && x.RevokedAt == null && x.ExpiresAt > now
            && db.Organizations.Any(o => o.Id == x.OrganizationId && o.ReviewStatus == "Approved"), ct);
    }
}
