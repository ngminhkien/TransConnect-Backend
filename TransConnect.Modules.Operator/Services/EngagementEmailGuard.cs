using Microsoft.EntityFrameworkCore;
using TransConnect.Common.Auth;
using TransConnect.Common.Notifications;
using TransConnect.Modules.Operator.Persistence;

namespace TransConnect.Modules.Operator.Services;

public sealed class EngagementEmailGuard(OperatorDbContext db, IAccountDirectory accounts, TimeProvider time) : IEmailDeliveryGuard
{
    public string SourceType => EmailSourceTypes.Engagement;
    public async Task<bool> CanDeliverAsync(Guid sourceId, string recipient, CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var accountId = await (from e in db.Engagements.AsNoTracking()
            join d in db.Drivers on e.DriverId equals d.Id
            where e.Id == sourceId && e.AcceptedAt == null && e.EndedAt == null && e.RevokedAt == null
                && e.ExpiresAt > now && d.ReviewStatus == "Approved"
                && db.Organizations.Any(o => o.Id == e.OrganizationId && o.ReviewStatus == "Approved")
            select (Guid?)d.AccountId).SingleOrDefaultAsync(ct);
        if (accountId is null) return false;
        var account = await accounts.FindActiveAsync(accountId.Value, ct);
        return account is not null && account.Email == recipient;
    }
}
