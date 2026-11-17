using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Transport;
using TransConnect.Modules.Transport.Dto;
using TransConnect.Modules.Transport.Entities;
using TransConnect.Modules.Transport.Persistence;

namespace TransConnect.Modules.Transport.Services;

public sealed class RouteService(TransportDbContext db, TransportAccess access, ISharedLocationCatalog locations)
{
    private const int MaxStops = 50;

    public async Task<IReadOnlyList<RouteResponse>> ListAsync(Guid organizationId, CancellationToken ct)
    {
        await access.ReaderAsync(organizationId, ct);
        var routes = await db.Routes.AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Status).ThenBy(x => x.Code).ToListAsync(ct);
        return await MapAsync(routes, ct);
    }

    public async Task<RouteDetailResponse> GetAsync(Guid organizationId, Guid id, CancellationToken ct)
    {
        await access.ReaderAsync(organizationId, ct);
        return await DetailAsync(await FindAsync(organizationId, id, false, ct), ct);
    }

    public async Task<RouteDetailResponse> CreateAsync(Guid organizationId, SaveRouteRequest request, CancellationToken ct)
    {
        var member = await access.MemberAsync(organizationId, ct);
        var registration = await ScheduleRules.ApprovedCoachAsync(db, organizationId, ct);
        var code = Code(request.Code);
        var name = Guard.Required(request.Name, 255, "name");
        if (await db.Routes.AnyAsync(x => x.OrganizationId == organizationId && x.Code == code, ct))
            throw new ConflictException("Mã tuyến đã tồn tại trong tổ chức.");
        var route = new TransportRoute { OrganizationId = organizationId, ServiceRegistrationId = registration.Id, Code = code, Name = name };
        db.Routes.Add(route);
        db.Revisions.Add(new RouteRevision { RouteId = route.Id, RevisionNo = 1, CreatedBy = member.AccountId });
        await Guard.SaveAsync(db, ct);
        return await DetailAsync(route, ct);
    }

    public async Task<RouteDetailResponse> UpdateAsync(Guid organizationId, Guid id, SaveRouteRequest request, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var route = await FindAsync(organizationId, id, true, ct);
        Guard.Version(request.Version, route.Version);
        await RequireWritableAsync(route, ct);
        var code = Code(request.Code);
        if (code != route.Code && await db.Routes.AnyAsync(x => x.OrganizationId == organizationId && x.Code == code && x.Id != id, ct))
            throw new ConflictException("Mã tuyến đã tồn tại trong tổ chức.");
        route.Code = code; route.Name = Guard.Required(request.Name, 255, "name"); route.Version++;
        await Guard.SaveAsync(db, ct);
        return await DetailAsync(route, ct);
    }

    public async Task<RouteDetailResponse> CreateDraftAsync(Guid organizationId, Guid id, CancellationToken ct)
    {
        var member = await access.MemberAsync(organizationId, ct);
        var route = await FindAsync(organizationId, id, true, ct);
        await RequireWritableAsync(route, ct);
        if (await db.Revisions.AnyAsync(x => x.RouteId == id && x.Status == RevisionStatuses.Draft, ct))
            throw new ConflictException("Tuyến đã có bản nháp đang soạn.");
        var next = await db.Revisions.Where(x => x.RouteId == id).MaxAsync(x => x.RevisionNo, ct) + 1;
        var draft = new RouteRevision { RouteId = id, RevisionNo = next, CreatedBy = member.AccountId };
        db.Revisions.Add(draft);
        if (route.CurrentRevisionId is { } currentId)
        {
            foreach (var stop in await db.Stops.AsNoTracking().Where(x => x.RevisionId == currentId).ToListAsync(ct))
            {
                stop.Id = Guid.NewGuid(); stop.RevisionId = draft.Id;
                db.Stops.Add(stop);
            }
        }
        await Guard.SaveAsync(db, ct);
        return await DetailAsync(route, ct);
    }

    public async Task<RouteDetailResponse> SaveDraftAsync(Guid organizationId, Guid id, Guid revisionId, SaveDraftRequest request, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var route = await FindAsync(organizationId, id, true, ct);
        await RequireWritableAsync(route, ct);
        var draft = await DraftAsync(id, revisionId, ct);
        Guard.Version(request.Version, draft.Version);
        Guard.Check(request.Stops.Count <= MaxStops, "stops", $"Tối đa {MaxStops} điểm dừng.");
        for (var i = 0; i < request.Stops.Count; i++)
        {
            var s = request.Stops[i];
            Guard.Check(s.SharedLocationId != Guid.Empty, $"stops[{i}].sharedLocationId", "Chọn điểm chung VieGo.");
            Guard.Check(s.OffsetMinutes is >= 0 and <= 7 * 24 * 60, $"stops[{i}].offsetMinutes", "Thời gian từ điểm đầu không hợp lệ.");
            Guard.Check(s.DwellMinutes is null or (>= 0 and <= 600), $"stops[{i}].dwellMinutes", "Thời gian dừng không hợp lệ.");
            Guard.Check(s.AllowPickup || s.AllowDropoff, $"stops[{i}]", "Mỗi điểm phải cho đón, trả hoặc cả hai.");
        }
        Guard.Check(request.Stops.Select(x => x.SharedLocationId).Distinct().Count() == request.Stops.Count, "stops",
            "Một điểm chỉ xuất hiện một lần trong tuyến.");
        var snapshots = await ActiveSnapshotsAsync(request.Stops.Select(x => x.SharedLocationId).ToList(), ct);
        db.Stops.RemoveRange(await db.Stops.Where(x => x.RevisionId == draft.Id).ToListAsync(ct));
        for (var i = 0; i < request.Stops.Count; i++)
        {
            var s = request.Stops[i];
            var stop = new RouteRevisionStop
            {
                RevisionId = draft.Id, Sequence = i + 1, SharedLocationId = s.SharedLocationId, AllowPickup = s.AllowPickup,
                AllowDropoff = s.AllowDropoff, OffsetMinutes = s.OffsetMinutes, DwellMinutes = s.DwellMinutes
            };
            Apply(stop, snapshots[s.SharedLocationId]);
            db.Stops.Add(stop);
        }
        draft.Note = Guard.Optional(request.Note, 1000, "note");
        draft.Version++;
        await Guard.SaveAsync(db, ct);
        return await DetailAsync(route, ct);
    }

    public async Task<RouteDetailResponse> PublishAsync(Guid organizationId, Guid id, Guid revisionId, VersionRequest request, CancellationToken ct)
    {
        var member = await access.MemberAsync(organizationId, ct);
        var route = await FindAsync(organizationId, id, true, ct);
        await RequireWritableAsync(route, ct);
        if (route.Status != RouteStatuses.Active) throw new ConflictException("Tuyến đang ngừng hoạt động, chưa thể công bố.");
        var draft = await DraftAsync(id, revisionId, ct);
        Guard.Version(request.Version, draft.Version);
        var stops = await db.Stops.Where(x => x.RevisionId == draft.Id).OrderBy(x => x.Sequence).ToListAsync(ct);
        ValidateForPublish(stops);
        // Chụp lại snapshot tại thời điểm công bố; điểm đã ngừng dùng chặn công bố.
        var snapshots = await ActiveSnapshotsAsync(stops.Select(x => x.SharedLocationId).ToList(), ct);
        foreach (var stop in stops) Apply(stop, snapshots[stop.SharedLocationId]);
        if (route.CurrentRevisionId is { } currentId)
        {
            var current = await db.Revisions.SingleAsync(x => x.Id == currentId, ct);
            current.Status = RevisionStatuses.Retired; current.Version++;
        }
        draft.Status = RevisionStatuses.Published; draft.PublishedAt = DateTime.UtcNow; draft.PublishedBy = member.AccountId;
        draft.Version++;
        route.CurrentRevisionId = draft.Id; route.Version++;
        await Guard.SaveAsync(db, ct);
        return await DetailAsync(route, ct);
    }

    public async Task<RouteDetailResponse?> DeleteDraftAsync(Guid organizationId, Guid id, Guid revisionId, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var route = await FindAsync(organizationId, id, true, ct);
        var draft = await DraftAsync(id, revisionId, ct);
        db.Stops.RemoveRange(await db.Stops.Where(x => x.RevisionId == draft.Id).ToListAsync(ct));
        db.Revisions.Remove(draft);
        // Tuyến chưa từng công bố: xóa luôn tuyến (soft delete).
        if (route.CurrentRevisionId is null) route.DeletedAt = DateTime.UtcNow;
        await Guard.SaveAsync(db, ct);
        return route.CurrentRevisionId is null ? null : await DetailAsync(route, ct);
    }

    public Task<RouteDetailResponse> DeactivateAsync(Guid organizationId, Guid id, Dto.NoteRequest request, CancellationToken ct)
        => SetStatusAsync(organizationId, id, request, false, false, ct);

    public Task<RouteDetailResponse> ActivateAsync(Guid organizationId, Guid id, Dto.NoteRequest request, CancellationToken ct)
        => SetStatusAsync(organizationId, id, request, true, false, ct);

    public async Task<IReadOnlyList<RouteResponse>> AdminListAsync(Guid organizationId, CancellationToken ct)
    {
        await access.AdminAsync(ct);
        return await MapAsync(await db.Routes.AsNoTracking().Where(x => x.OrganizationId == organizationId)
            .OrderBy(x => x.Code).ToListAsync(ct), ct);
    }

    public async Task<RouteDetailResponse> AdminDeactivateAsync(Guid id, Dto.NoteRequest request, CancellationToken ct)
    {
        await access.AdminAsync(ct);
        var route = await db.Routes.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Không tìm thấy tuyến.");
        return await SetStatusAsync(route.OrganizationId, id, request, false, true, ct);
    }

    private async Task<RouteDetailResponse> SetStatusAsync(Guid organizationId, Guid id, Dto.NoteRequest request, bool active,
        bool byAdmin, CancellationToken ct)
    {
        if (!byAdmin) await access.MemberAsync(organizationId, ct);
        var route = await FindAsync(organizationId, id, true, ct);
        Guard.Version(request.Version, route.Version);
        var note = Guard.Optional(request.Note, 1000, "note");
        if (active)
        {
            if (route.Status == RouteStatuses.Active) throw new ConflictException("Tuyến đang hoạt động.");
            await RequireWritableAsync(route, ct);
            route.Status = RouteStatuses.Active; route.StatusReason = note;
        }
        else
        {
            if (route.Status == RouteStatuses.Inactive) throw new ConflictException("Tuyến đã ngừng hoạt động.");
            if (byAdmin) Guard.Check(note is not null, "note", "Vui lòng nhập lý do ngừng tuyến.");
            route.Status = RouteStatuses.Inactive;
            route.StatusReason = byAdmin ? "Admin ngừng tuyến: " + note : note;
            await ScheduleRules.PauseForRouteAsync(db, route.Id, "Tuyến ngừng hoạt động.", ct);
        }
        route.Version++;
        await Guard.SaveAsync(db, ct);
        return await DetailAsync(route, ct);
    }

    private static void ValidateForPublish(IReadOnlyList<RouteRevisionStop> stops)
    {
        Guard.Check(stops.Count >= 2, "stops", "Tuyến cần ít nhất 2 điểm dừng.");
        Guard.Check(stops[0].AllowPickup, "stops[0]", "Điểm đầu phải cho đón khách.");
        Guard.Check(stops[^1].AllowDropoff, $"stops[{stops.Count - 1}]", "Điểm cuối phải cho trả khách.");
        Guard.Check(stops[0].OffsetMinutes == 0, "stops[0].offsetMinutes", "Điểm đầu có thời gian 0 phút.");
        for (var i = 1; i < stops.Count; i++)
            Guard.Check(stops[i].OffsetMinutes > stops[i - 1].OffsetMinutes, $"stops[{i}].offsetMinutes",
                "Thời gian đến các điểm phải tăng dần.");
    }

    private async Task<IReadOnlyDictionary<Guid, SharedLocationSnapshot>> ActiveSnapshotsAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var snapshots = await locations.GetActiveAsync(ids, ct);
        for (var i = 0; i < ids.Count; i++)
            Guard.Check(snapshots.ContainsKey(ids[i]), $"stops[{i}].sharedLocationId", "Điểm không còn trong danh mục VieGo đang dùng.");
        return snapshots;
    }

    private static void Apply(RouteRevisionStop stop, SharedLocationSnapshot s)
    {
        stop.LocationVersion = s.Version; stop.Name = s.Name; stop.AddressDetail = s.AddressDetail;
        stop.ProvinceCode = s.ProvinceCode; stop.WardCode = s.WardCode; stop.Latitude = s.Latitude; stop.Longitude = s.Longitude;
    }

    private static string Code(string? value)
    {
        var code = Guard.Required(value, 50, "code").ToUpperInvariant();
        Guard.Check(System.Text.RegularExpressions.Regex.IsMatch(code, @"\A[A-Z0-9][A-Z0-9_\-\.]*\z"), "code",
            "Mã tuyến chỉ gồm chữ, số, '-', '_' hoặc '.'.");
        return code;
    }

    private async Task RequireWritableAsync(TransportRoute route, CancellationToken ct)
    {
        if (!await ScheduleRules.RegistrationApprovedAsync(db, route.ServiceRegistrationId, ct))
            throw new ConflictException("Dịch vụ của tuyến không còn được duyệt; tuyến chỉ xem được.");
    }

    private async Task<TransportRoute> FindAsync(Guid organizationId, Guid id, bool tracked, CancellationToken ct)
    {
        var query = tracked ? db.Routes : db.Routes.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct)
            ?? throw new NotFoundException("Không tìm thấy tuyến.");
    }

    private async Task<RouteRevision> DraftAsync(Guid routeId, Guid revisionId, CancellationToken ct)
    {
        var revision = await db.Revisions.SingleOrDefaultAsync(x => x.Id == revisionId && x.RouteId == routeId, ct)
            ?? throw new NotFoundException("Không tìm thấy bản tuyến.");
        if (revision.Status != RevisionStatuses.Draft) throw new ConflictException("Bản tuyến đã công bố không thể sửa; hãy tạo bản mới.");
        return revision;
    }

    private async Task<List<RouteResponse>> MapAsync(IReadOnlyList<TransportRoute> routes, CancellationToken ct)
    {
        var ids = routes.Select(x => x.Id).ToList();
        var revisions = await db.Revisions.AsNoTracking().Where(x => ids.Contains(x.RouteId)).ToListAsync(ct);
        var currentIds = routes.Where(x => x.CurrentRevisionId != null).Select(x => x.CurrentRevisionId!.Value).ToList();
        var ends = (await db.Stops.AsNoTracking().Where(x => currentIds.Contains(x.RevisionId)).ToListAsync(ct))
            .GroupBy(x => x.RevisionId).ToDictionary(g => g.Key, g => (g.MinBy(s => s.Sequence)!.Name, g.MaxBy(s => s.Sequence)!.Name));
        return routes.Select(r =>
        {
            var current = revisions.FirstOrDefault(x => x.Id == r.CurrentRevisionId);
            var end = r.CurrentRevisionId is { } c && ends.TryGetValue(c, out var e) ? e : default;
            return new RouteResponse(r.Id, r.OrganizationId, r.ServiceRegistrationId, r.Code, r.Name, r.Status, r.StatusReason,
                r.CurrentRevisionId, current?.RevisionNo, revisions.Any(x => x.RouteId == r.Id && x.Status == RevisionStatuses.Draft),
                r.Version, end.Item1, end.Item2);
        }).ToList();
    }

    private async Task<RouteDetailResponse> DetailAsync(TransportRoute route, CancellationToken ct)
    {
        var summary = (await MapAsync([route], ct))[0];
        var revisions = await db.Revisions.AsNoTracking().Where(x => x.RouteId == route.Id).OrderByDescending(x => x.RevisionNo).ToListAsync(ct);
        var revisionIds = revisions.Select(x => x.Id).ToList();
        var stops = (await db.Stops.AsNoTracking().Where(x => revisionIds.Contains(x.RevisionId)).ToListAsync(ct))
            .GroupBy(x => x.RevisionId).ToDictionary(g => g.Key, g => g.OrderBy(x => x.Sequence).ToList());
        RevisionResponse Map(RouteRevision r, bool withStops)
        {
            var list = stops.GetValueOrDefault(r.Id) ?? [];
            return new RevisionResponse(r.Id, r.RevisionNo, r.Status, r.Note, r.CreatedAt, r.PublishedAt, r.Version, list.Count,
                withStops ? list.Select(s => new StopResponse(s.Id, s.Sequence, s.SharedLocationId, s.LocationVersion, s.Name,
                    s.AddressDetail, s.ProvinceCode, s.WardCode, s.Latitude, s.Longitude, s.AllowPickup, s.AllowDropoff,
                    s.OffsetMinutes, s.DwellMinutes)).ToList() : null);
        }
        var current = revisions.FirstOrDefault(x => x.Id == route.CurrentRevisionId);
        var draft = revisions.FirstOrDefault(x => x.Status == RevisionStatuses.Draft);
        return new RouteDetailResponse(summary, revisions.Select(x => Map(x, true)).ToList(),
            current is null ? null : Map(current, true), draft is null ? null : Map(draft, true));
    }
}
