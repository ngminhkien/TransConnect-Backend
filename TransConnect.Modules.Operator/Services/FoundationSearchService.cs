using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Common.Response;
using TransConnect.Modules.Operator.Dto.Requests;
using TransConnect.Modules.Operator.Dto.Responses;
using TransConnect.Modules.Operator.Persistence;

namespace TransConnect.Modules.Operator.Services;

public sealed record MemberSummary(Guid Id, Guid AccountId, string FullName, string Role, bool IsActive);
public sealed record EngagementSummary(Guid Id, Guid DriverId, string FullName, DateTime ExpiresAt,
    DateTime? AcceptedAt, DateTime? EndedAt, DateTime? RevokedAt);

public sealed class FoundationSearchService(OperatorDbContext db, OperatorAccess access, ICurrentUser current,
    IAccountDisplayDirectory names)
{
    public async Task<PagedResult<OrganizationResponse>> OrganizationsAsync(FoundationSearchRequest request, CancellationToken ct)
    {
        ProfileValidation.Check(request);
        var account = await access.AccountAsync(ct);
        var query = db.Organizations.AsNoTracking();
        if (account.Group != AccountGroups.InternalAdmin || !current.HasPermission("organizations.manage"))
            query = query.Where(x => x.CreatedByUserId == account.AccountId || db.Members.Any(m => m.OrganizationId == x.Id
                && m.AccountId == account.AccountId && m.IsActive));
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(x => x.Name.Contains(request.Search.Trim()) || x.RegistrationNumber.Contains(request.Search.Trim())
                || x.ProposedOwnerEmail.Contains(request.Search.Trim()));
        if (request.Status is not null) query = query.Where(x => x.ReviewStatus == request.Status);
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new OrganizationResponse(x.Id, x.Name, x.RegistrationNumber, x.ProposedOwnerEmail,
                x.ReviewStatus, x.ReviewNote, x.EmployeeLimit, x.DriverLimit, x.LegalEntityType,
                x.RegisteredAddress, x.ContactPhone, x.ContactEmail, x.LegalEntityType != null && x.RegisteredAddress != null
                    && x.ContactPhone != null && x.ContactEmail != null, x.CreatedByUserId)).ToListAsync(ct);
        return new(items, request.PageNumber, request.PageSize, count);
    }

    public async Task<PagedResult<DriverResponse>> DriversAsync(FoundationSearchRequest request, CancellationToken ct)
    {
        ProfileValidation.Check(request);
        await access.AdminAsync(ct);
        var query = db.Drivers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(x => x.FullName.Contains(request.Search.Trim()));
        if (request.Status is not null) query = query.Where(x => x.ReviewStatus == request.Status);
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.FullName).ThenBy(x => x.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new DriverResponse(x.Id, x.AccountId, x.FullName, x.ReviewStatus, x.ReviewNote,
                x.ContactPhone, x.ContactAddress, x.ContactPhone != null && x.ContactAddress != null)).ToListAsync(ct);
        return new(items, request.PageNumber, request.PageSize, count);
    }

    public async Task<DriverResponse> DriverAsync(Guid id, CancellationToken ct)
    {
        await access.AdminAsync(ct);
        return await db.Drivers.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new DriverResponse(x.Id, x.AccountId, x.FullName, x.ReviewStatus, x.ReviewNote,
                x.ContactPhone, x.ContactAddress, x.ContactPhone != null && x.ContactAddress != null)).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Tài xế", id);
    }

    public async Task<PagedResult<MemberSummary>> MembersAsync(Guid id, FoundationSearchRequest request, CancellationToken ct)
    {
        ProfileValidation.Check(request);
        await access.OrganizationAsync(id, "owner", ct);
        var query = db.Members.AsNoTracking().Where(x => x.OrganizationId == id);
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        var directory = await names.NamesAsync(items.Select(x => x.AccountId).ToArray(), ct);
        return new(items.Select(x => new MemberSummary(x.Id, x.AccountId,
            directory.GetValueOrDefault(x.AccountId, "Tài khoản không còn khả dụng"), x.Role, x.IsActive)).ToList(),
            request.PageNumber, request.PageSize, count);
    }

    public async Task<PagedResult<EngagementSummary>> EngagementsAsync(Guid id, FoundationSearchRequest request, CancellationToken ct)
    {
        ProfileValidation.Check(request);
        await access.OrganizationAsync(id, "read", ct);
        var query = from e in db.Engagements.AsNoTracking() join d in db.Drivers on e.DriverId equals d.Id
            where e.OrganizationId == id select new { e.Id, e.DriverId, FullName = e.EndedAt != null ? e.DriverNameSnapshot : d.FullName,
                e.CreatedAt, e.ExpiresAt, e.AcceptedAt, e.EndedAt, e.RevokedAt };
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new EngagementSummary(x.Id, x.DriverId, x.FullName ?? "Không có snapshot", x.ExpiresAt,
                x.AcceptedAt, x.EndedAt, x.RevokedAt)).ToListAsync(ct);
        return new(items, request.PageNumber, request.PageSize, count);
    }

    public async Task<PagedResult<AuditResponse>> AuditAsync(Guid id, FoundationSearchRequest request, CancellationToken ct)
    {
        ProfileValidation.Check(request);
        await access.OrganizationAsync(id, "owner", ct);
        var query = db.AuditLogs.AsNoTracking().Where(x => x.OrganizationId == id);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((request.PageNumber - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new AuditResponse(x.Id, x.ActorAccountId, x.SubjectId, x.Action, x.Details, x.CreatedAt,
                x.SubjectType, x.RequestId)).ToListAsync(ct);
        return new(items, request.PageNumber, request.PageSize, count);
    }
}
