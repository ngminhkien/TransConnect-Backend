using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Modules.Operator.Persistence;

namespace TransConnect.Modules.Operator.Services;

public sealed record ReviewHistoryItem(Guid Id, string Status, string? Note, DateTime CreatedAt);
public sealed record InvitationItem(Guid Id, Guid OrganizationId, string OrganizationName, string Role,
    DateTime ExpiresAt, DateTime? AcceptedAt, DateTime? RevokedAt, string Email);
public sealed record MembershipContext(Guid OrganizationId, string OrganizationName, string Role, bool IsActive,
    int EmployeeLimit, int? DriverLimit, int EmployeeOccupancy, int DriverOccupancy, IReadOnlyList<string> Capabilities);
public sealed record FoundationContext(IReadOnlyList<MembershipContext> Memberships, Guid? DriverId, string? DriverStatus);
public sealed record DriverCollaboration(Guid Id, Guid OrganizationId, string OrganizationName,
    DateTime ExpiresAt, DateTime? AcceptedAt, DateTime? EndedAt, DateTime? RevokedAt);

public sealed class FoundationReadService(OperatorDbContext db, OperatorAccess access, IOrganizationService organizations)
{
    public async Task<FoundationContext> ContextAsync(CancellationToken ct)
    {
        var account = await access.OperatorAsync(ct);
        var now = DateTime.UtcNow;
        var members = await (from m in db.Members.AsNoTracking()
            join o in db.Organizations on m.OrganizationId equals o.Id
            where m.AccountId == account.AccountId
            select new { o.Id, o.Name, o.ReviewStatus, m.Role, m.IsActive, o.EmployeeLimit, o.DriverLimit,
                EmployeeOccupancy = db.Members.Count(x => x.OrganizationId == o.Id && x.IsActive)
                    + db.Invitations.Count(x => x.OrganizationId == o.Id && x.Role != "Driver"
                        && x.AcceptedAt == null && x.RevokedAt == null && x.ExpiresAt > now),
                DriverOccupancy = db.Engagements.Count(x => x.OrganizationId == o.Id && x.EndedAt == null
                    && x.RevokedAt == null && (x.AcceptedAt != null || x.ExpiresAt > now))
                    + db.Invitations.Count(x => x.OrganizationId == o.Id && x.Role == "Driver"
                        && x.AcceptedAt == null && x.RevokedAt == null && x.ExpiresAt > now) }).ToListAsync(ct);
        var driver = await db.Drivers.AsNoTracking().SingleOrDefaultAsync(x => x.AccountId == account.AccountId, ct);
        return new(members.Select(x => new MembershipContext(x.Id, x.Name, x.Role, x.IsActive, x.EmployeeLimit,
            x.DriverLimit, x.EmployeeOccupancy, x.DriverOccupancy,
            !x.IsActive || x.ReviewStatus != "Approved" ? [] : x.Role == "OrganizationOwner" ? ["read", "manage", "owner"] :
                x.Role == "Manager" ? ["read", "manage"] : ["read"])).ToList(), driver?.Id, driver?.ReviewStatus);
    }

    public async Task<IReadOnlyList<InvitationItem>> InvitationsAsync(Guid? organizationId, CancellationToken ct)
    {
        var account = organizationId is null ? await access.OperatorAsync(ct) : await access.AccountAsync(ct);
        var query = db.Invitations.AsNoTracking();
        if (organizationId is { } id)
        {
            await access.OrganizationAsync(id, "owner", ct);
            query = query.Where(x => x.OrganizationId == id);
        }
        else query = query.Where(x => x.Email == account.Email.ToLower());
        return await (from i in query join o in db.Organizations on i.OrganizationId equals o.Id
            orderby i.CreatedAt descending
            select new InvitationItem(i.Id, i.OrganizationId, o.Name, i.Role, i.ExpiresAt, i.AcceptedAt, i.RevokedAt, i.Email))
            .Take(200).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DriverCollaboration>> CollaborationsAsync(CancellationToken ct)
    {
        var account = await access.OperatorAsync(ct);
        return await (from e in db.Engagements.AsNoTracking()
            join d in db.Drivers on e.DriverId equals d.Id
            join o in db.Organizations on e.OrganizationId equals o.Id
            where d.AccountId == account.AccountId
            orderby e.CreatedAt descending
            select new DriverCollaboration(e.Id, o.Id, o.Name, e.ExpiresAt, e.AcceptedAt, e.EndedAt, e.RevokedAt))
            .Take(200).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReviewHistoryItem>> HistoryAsync(string type, Guid id, CancellationToken ct)
    {
        if (type == "Organization") await organizations.GetAsync(id, ct);
        else
        {
            var account = await access.AccountAsync(ct);
            if (!await db.Drivers.AnyAsync(x => x.Id == id && x.AccountId == account.AccountId, ct))
                await access.AdminAsync(ct);
        }
        return await db.ProfileReviews.AsNoTracking().Where(x => x.SubjectType == type && x.SubjectId == id)
            .OrderByDescending(x => x.CreatedAt).Select(x => new ReviewHistoryItem(x.Id, x.Status, x.Note, x.CreatedAt))
            .ToListAsync(ct);
    }
}
