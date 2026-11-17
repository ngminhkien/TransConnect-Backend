using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Response;
using TransConnect.Common.Transport;
using TransConnect.Modules.Transport.Dto;
using TransConnect.Modules.Transport.Entities;
using TransConnect.Modules.Transport.Persistence;

namespace TransConnect.Modules.Transport.Services;

public sealed class ServiceRegistrationService(TransportDbContext db, TransportAccess access) : IServiceRegistrationIntake
{
    public async Task<IReadOnlyList<ServiceTypeResponse>> TypesAsync(CancellationToken ct)
    {
        await access.ActorAsync(ct);
        return (await db.ServiceTypes.AsNoTracking().OrderBy(x => x.SortOrder).ToListAsync(ct))
            .Select(x => new ServiceTypeResponse(x.Code, x.Name, x.Description,
                x.AllowedModes.Split(',', StringSplitOptions.RemoveEmptyEntries), x.IsOperational)).ToList();
    }

    public async Task<IReadOnlyList<RegistrationResponse>> ForOrganizationAsync(Guid organizationId, CancellationToken ct)
    {
        await access.ReaderAsync(organizationId, ct);
        var rows = await db.Registrations.AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Status == RegistrationStatuses.Withdrawn).ThenByDescending(x => x.SubmittedAt).ToListAsync(ct);
        return await MapAsync(rows, false, ct);
    }

    public async Task<RegistrationResponse> SubmitAsync(Guid organizationId, SubmitRegistrationRequest request, CancellationToken ct)
    {
        var owner = await access.OwnerAsync(organizationId, ct);
        var code = Guard.Required(request.ServiceTypeCode, 50, "serviceTypeCode");
        Guard.Check(ServiceTypeCodes.IsKnown(code), "serviceTypeCode", "Loại dịch vụ không hợp lệ.");
        var scope = Guard.Optional(request.ScopeNote, 2000, "scopeNote");
        if (await db.Registrations.AnyAsync(x => x.OrganizationId == organizationId && x.ServiceTypeCode == code
                && x.Status != RegistrationStatuses.Withdrawn, ct))
            throw new ConflictException("Tổ chức đã có đăng ký cho loại dịch vụ này.");
        var registration = new ServiceRegistration
        {
            OrganizationId = organizationId, ServiceTypeCode = code, ScopeNote = scope, SubmittedBy = owner.AccountId
        };
        db.Registrations.Add(registration);
        Record(registration, "Submitted", owner.AccountId, null);
        await Guard.SaveAsync(db, ct);
        return (await MapAsync([registration], true, ct))[0];
    }

    public async Task<RegistrationResponse> ResubmitAsync(Guid organizationId, Guid id, ResubmitRegistrationRequest request, CancellationToken ct)
    {
        var owner = await access.OwnerAsync(organizationId, ct);
        var registration = await FindAsync(id, organizationId, ct);
        Guard.Version(request.Version, registration.Version);
        if (registration.Status != RegistrationStatuses.Rejected) throw new ConflictException("Chỉ nộp lại đăng ký bị từ chối.");
        registration.ScopeNote = Guard.Optional(request.ScopeNote, 2000, "scopeNote");
        registration.Status = RegistrationStatuses.Pending;
        registration.SubmittedAt = DateTime.UtcNow; registration.SubmittedBy = owner.AccountId;
        registration.Version++;
        Record(registration, "Resubmitted", owner.AccountId, null);
        await Guard.SaveAsync(db, ct);
        return (await MapAsync([registration], true, ct))[0];
    }

    public async Task<RegistrationResponse> WithdrawAsync(Guid organizationId, Guid id, NoteRequest request, CancellationToken ct)
    {
        var owner = await access.OwnerAsync(organizationId, ct);
        var registration = await FindAsync(id, organizationId, ct);
        Guard.Version(request.Version, registration.Version);
        if (registration.Status == RegistrationStatuses.Withdrawn) throw new ConflictException("Đăng ký đã được rút.");
        var note = Guard.Optional(request.Note, 2000, "note");
        registration.Status = RegistrationStatuses.Withdrawn;
        registration.Version++;
        Record(registration, "Withdrawn", owner.AccountId, note);
        await ScheduleRules.PauseForRegistrationAsync(db, registration.Id, "Đăng ký dịch vụ đã được rút.", ct);
        await Guard.SaveAsync(db, ct);
        return (await MapAsync([registration], true, ct))[0];
    }

    public async Task<PagedResult<RegistrationResponse>> AdminListAsync(string? status, string? serviceTypeCode, string? search,
        int pageNumber, int pageSize, CancellationToken ct)
    {
        await access.AdminAsync(ct);
        Guard.Check(pageNumber >= 1, "pageNumber", "Trang phải từ 1.");
        Guard.Check(pageSize is >= 1 and <= 100, "pageSize", "Kích thước trang từ 1 đến 100.");
        Guard.Check(search is null || search.Length <= 100, "search", "Tối đa 100 ký tự.");
        var query = db.Registrations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(serviceTypeCode)) query = query.Where(x => x.ServiceTypeCode == serviceTypeCode);
        // Tên tổ chức thuộc Operator: lọc trong bộ nhớ trên tập mới nhất có giới hạn.
        var rows = await query.OrderBy(x => x.Status == RegistrationStatuses.Pending ? 0 : 1)
            .ThenByDescending(x => x.SubmittedAt).Take(1000).ToListAsync(ct);
        var mapped = await MapAsync(rows, false, ct);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            mapped = mapped.Where(x => (x.OrganizationName ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                || (x.RegistrationNumber ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return new(mapped.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(), pageNumber, pageSize, mapped.Count);
    }

    public async Task<RegistrationResponse> AdminDetailAsync(Guid id, CancellationToken ct)
    {
        await access.AdminAsync(ct);
        var registration = await db.Registrations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy đăng ký dịch vụ.");
        return (await MapAsync([registration], true, ct))[0];
    }

    public async Task<RegistrationResponse> ReviewAsync(Guid id, ReviewRegistrationRequest request, CancellationToken ct)
    {
        var admin = await access.AdminAsync(ct);
        Guard.Check(request.Decision is RegistrationStatuses.Approved or RegistrationStatuses.Rejected, "decision", "Chọn duyệt hoặc từ chối.");
        var note = Guard.Optional(request.Note, 2000, "note");
        if (request.Decision == RegistrationStatuses.Rejected) Guard.Check(note is not null, "note", "Vui lòng nhập lý do từ chối.");
        var registration = await db.Registrations.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy đăng ký dịch vụ.");
        Guard.Version(request.Version, registration.Version);
        if (registration.Status != RegistrationStatuses.Pending) throw new ConflictException("Đăng ký không ở trạng thái chờ duyệt.");
        var organization = await access.Organizations.FindAsync(registration.OrganizationId, ct);
        if (request.Decision == RegistrationStatuses.Approved && organization?.ReviewStatus != "Approved")
            throw new ConflictException("Tổ chức chưa được duyệt nên chưa thể duyệt dịch vụ.");
        registration.Status = request.Decision;
        registration.ReviewNote = note; registration.ReviewedBy = admin; registration.ReviewedAt = DateTime.UtcNow;
        registration.Version++;
        Record(registration, request.Decision, admin, note);
        await Guard.SaveAsync(db, ct);
        return (await MapAsync([registration], true, ct))[0];
    }

    public async Task<RegistrationResponse> SuspendAsync(Guid id, NoteRequest request, CancellationToken ct)
    {
        var admin = await access.AdminAsync(ct);
        var note = Guard.Optional(request.Note, 2000, "note");
        Guard.Check(note is not null, "note", "Vui lòng nhập lý do tạm ngưng.");
        var registration = await db.Registrations.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy đăng ký dịch vụ.");
        Guard.Version(request.Version, registration.Version);
        if (registration.Status != RegistrationStatuses.Approved) throw new ConflictException("Chỉ tạm ngưng dịch vụ đang được duyệt.");
        registration.Status = RegistrationStatuses.Suspended;
        registration.ReviewNote = note; registration.ReviewedBy = admin; registration.ReviewedAt = DateTime.UtcNow;
        registration.Version++;
        Record(registration, "Suspended", admin, note);
        await ScheduleRules.PauseForRegistrationAsync(db, registration.Id, "Dịch vụ bị tạm ngưng: " + note, ct);
        await Guard.SaveAsync(db, ct);
        return (await MapAsync([registration], true, ct))[0];
    }

    public async Task<RegistrationResponse> ReinstateAsync(Guid id, NoteRequest request, CancellationToken ct)
    {
        var admin = await access.AdminAsync(ct);
        var note = Guard.Optional(request.Note, 2000, "note");
        var registration = await db.Registrations.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Không tìm thấy đăng ký dịch vụ.");
        Guard.Version(request.Version, registration.Version);
        if (registration.Status != RegistrationStatuses.Suspended) throw new ConflictException("Chỉ khôi phục dịch vụ đang tạm ngưng.");
        registration.Status = RegistrationStatuses.Approved;
        registration.ReviewNote = note; registration.ReviewedBy = admin; registration.ReviewedAt = DateTime.UtcNow;
        registration.Version++;
        // Lịch đã tạm dừng giữ nguyên Paused; nhà xe chủ động kích hoạt lại.
        Record(registration, "Reinstated", admin, note);
        await Guard.SaveAsync(db, ct);
        return (await MapAsync([registration], true, ct))[0];
    }

    // Gọi từ Operator sau khi duyệt tổ chức (Q-M2-SERVICE-007B). Không bao giờ tự Approved.
    public async Task CreatePendingAsync(Guid organizationId, IReadOnlyCollection<string> serviceTypeCodes, Guid actorAccountId,
        CancellationToken ct = default)
    {
        var codes = serviceTypeCodes.Where(ServiceTypeCodes.IsKnown).Distinct().ToList();
        if (codes.Count == 0) return;
        var existing = await db.Registrations.Where(x => x.OrganizationId == organizationId
            && x.Status != RegistrationStatuses.Withdrawn).Select(x => x.ServiceTypeCode).ToListAsync(ct);
        foreach (var code in codes.Except(existing))
        {
            var registration = new ServiceRegistration
            {
                OrganizationId = organizationId, ServiceTypeCode = code, SubmittedBy = actorAccountId,
                ScopeNote = "Khai báo khi nộp hồ sơ tổ chức."
            };
            db.Registrations.Add(registration);
            Record(registration, "Declared", actorAccountId, null);
        }
        await Guard.SaveAsync(db, ct);
    }

    private async Task<ServiceRegistration> FindAsync(Guid id, Guid organizationId, CancellationToken ct)
        => await db.Registrations.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct)
           ?? throw new NotFoundException("Không tìm thấy đăng ký dịch vụ.");

    private void Record(ServiceRegistration registration, string action, Guid actor, string? note)
        => db.RegistrationEvents.Add(new ServiceRegistrationEvent
        {
            RegistrationId = registration.Id, Action = action, Status = registration.Status, Note = note,
            ScopeNote = registration.ScopeNote, ActorAccountId = actor
        });

    private async Task<List<RegistrationResponse>> MapAsync(IReadOnlyList<ServiceRegistration> rows, bool withEvents, CancellationToken ct)
    {
        var types = await db.ServiceTypes.AsNoTracking().ToDictionaryAsync(x => x.Code, ct);
        var organizations = await access.Organizations.FindManyAsync(rows.Select(x => x.OrganizationId).Distinct().ToList(), ct);
        var events = new Dictionary<Guid, List<RegistrationEventResponse>>();
        if (withEvents)
        {
            var ids = rows.Select(x => x.Id).ToList();
            // Sự kiện vừa thêm trong cùng request đã được lưu trước khi map.
            events = (await db.RegistrationEvents.AsNoTracking().Where(x => ids.Contains(x.RegistrationId)).ToListAsync(ct))
                .OrderBy(x => x.CreatedAt)
                .GroupBy(x => x.RegistrationId)
                .ToDictionary(g => g.Key, g => g.Select(e => new RegistrationEventResponse(e.Id, e.Action, e.Status, e.Note,
                    e.ScopeNote, e.ActorAccountId, e.CreatedAt)).ToList());
        }
        return rows.Select(x =>
        {
            var type = types.GetValueOrDefault(x.ServiceTypeCode);
            var org = organizations.GetValueOrDefault(x.OrganizationId);
            return new RegistrationResponse(x.Id, x.OrganizationId, org?.Name, org?.RegistrationNumber, x.ServiceTypeCode,
                type?.Name ?? x.ServiceTypeCode, type?.IsOperational ?? false, x.Status, x.ScopeNote, x.ReviewNote, x.ReviewedAt,
                x.SubmittedAt, x.Version, withEvents ? events.GetValueOrDefault(x.Id) ?? [] : null);
        }).ToList();
    }
}
