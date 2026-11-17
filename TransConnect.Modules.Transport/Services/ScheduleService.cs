using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Transport.Dto;
using TransConnect.Modules.Transport.Entities;
using TransConnect.Modules.Transport.Persistence;

namespace TransConnect.Modules.Transport.Services;

public sealed class ScheduleService(TransportDbContext db, TransportAccess access, SeatLayoutService seats)
{
    public async Task<IReadOnlyList<ScheduleResponse>> ListAsync(Guid organizationId, Guid? routeId, CancellationToken ct)
    {
        await access.ReaderAsync(organizationId, ct);
        var query = db.Schedules.AsNoTracking().Where(x => x.OrganizationId == organizationId);
        if (routeId is { } r) query = query.Where(x => x.RouteId == r);
        var rows = await query.OrderBy(x => x.Status == ScheduleStatuses.Ended).ThenBy(x => x.DepartureMinutes).ToListAsync(ct);
        return await MapAsync(organizationId, rows, ct);
    }

    public async Task<ScheduleResponse> GetAsync(Guid organizationId, Guid id, CancellationToken ct)
    {
        await access.ReaderAsync(organizationId, ct);
        return (await MapAsync(organizationId, [await FindAsync(organizationId, id, false, ct)], ct))[0];
    }

    public async Task<ScheduleResponse> CreateAsync(Guid organizationId, SaveScheduleRequest request, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var schedule = new Schedule { OrganizationId = organizationId };
        var reserved = await ApplyAsync(schedule, request, ct);
        db.Schedules.Add(schedule);
        AddSeats(schedule.Id, reserved);
        await Guard.SaveAsync(db, ct);
        return (await MapAsync(organizationId, [schedule], ct))[0];
    }

    public async Task<ScheduleResponse> UpdateAsync(Guid organizationId, Guid id, SaveScheduleRequest request, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var schedule = await FindAsync(organizationId, id, true, ct);
        Guard.Version(request.Version, schedule.Version);
        if (schedule.Status != ScheduleStatuses.Draft)
            throw new ConflictException("Lịch đã kích hoạt không sửa trực tiếp; hãy dùng chức năng thay lịch.");
        var reserved = await ApplyAsync(schedule, request, ct);
        db.VieGoSeats.RemoveRange(await db.VieGoSeats.Where(x => x.ScheduleId == id).ToListAsync(ct));
        AddSeats(id, reserved);
        schedule.Version++;
        await Guard.SaveAsync(db, ct);
        return (await MapAsync(organizationId, [schedule], ct))[0];
    }

    public async Task<ScheduleResponse> ActivateAsync(Guid organizationId, Guid id, ScheduleStatusRequest request, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var schedule = await FindAsync(organizationId, id, true, ct);
        Guard.Version(request.Version, schedule.Version);
        if (schedule.Status is not (ScheduleStatuses.Draft or ScheduleStatuses.Paused))
            throw new ConflictException("Chỉ kích hoạt lịch nháp hoặc đang tạm dừng.");
        await RequireRunnableAsync(schedule, schedule.Status == ScheduleStatuses.Draft, ct);
        var labels = await db.VieGoSeats.AsNoTracking().Where(x => x.ScheduleId == id).Select(x => x.SeatLabel).ToListAsync(ct);
        await ValidateSeatsAsync(schedule, labels, ct);
        await RequireNoOverlapAsync(schedule, null, ct);
        schedule.Status = ScheduleStatuses.Active; schedule.StatusReason = null; schedule.Version++;
        await Guard.SaveAsync(db, ct);
        return (await MapAsync(organizationId, [schedule], ct))[0];
    }

    public async Task<ScheduleResponse> PauseAsync(Guid organizationId, Guid id, ScheduleStatusRequest request, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var schedule = await FindAsync(organizationId, id, true, ct);
        Guard.Version(request.Version, schedule.Version);
        if (schedule.Status != ScheduleStatuses.Active) throw new ConflictException("Chỉ tạm dừng lịch đang chạy.");
        schedule.Status = ScheduleStatuses.Paused; schedule.StatusReason = Guard.Optional(request.Reason, 1000, "reason");
        schedule.Version++;
        await Guard.SaveAsync(db, ct);
        return (await MapAsync(organizationId, [schedule], ct))[0];
    }

    public async Task<ScheduleResponse> EndAsync(Guid organizationId, Guid id, ScheduleStatusRequest request, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var schedule = await FindAsync(organizationId, id, true, ct);
        Guard.Version(request.Version, schedule.Version);
        if (schedule.Status is not (ScheduleStatuses.Active or ScheduleStatuses.Paused))
            throw new ConflictException("Chỉ kết thúc lịch đang chạy hoặc tạm dừng; lịch nháp thì xóa.");
        schedule.Status = ScheduleStatuses.Ended; schedule.StatusReason = Guard.Optional(request.Reason, 1000, "reason");
        schedule.Version++;
        await Guard.SaveAsync(db, ct);
        return (await MapAsync(organizationId, [schedule], ct))[0];
    }

    public async Task DeleteDraftAsync(Guid organizationId, Guid id, int version, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var schedule = await FindAsync(organizationId, id, true, ct);
        Guard.Version(version, schedule.Version);
        if (schedule.Status != ScheduleStatuses.Draft) throw new ConflictException("Chỉ xóa lịch nháp.");
        schedule.DeletedAt = DateTime.UtcNow;
        await Guard.SaveAsync(db, ct);
    }

    // Lịch đang chạy không sửa đè: tạo lịch mới từ FromDate, lịch cũ kết thúc ngày trước đó (Q-07).
    public async Task<ScheduleResponse> ReplaceAsync(Guid organizationId, Guid id, ReplaceScheduleRequest request, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var old = await FindAsync(organizationId, id, true, ct);
        Guard.Version(request.Version, old.Version);
        if (old.Status is not (ScheduleStatuses.Active or ScheduleStatuses.Paused))
            throw new ConflictException("Chỉ thay lịch đang chạy hoặc tạm dừng.");
        Guard.Check(request.FromDate >= access.Today && request.FromDate > old.EffectiveFrom, "fromDate",
            "Ngày áp dụng lịch mới phải từ hôm nay và sau ngày bắt đầu của lịch cũ.");
        Guard.Check(old.EffectiveTo is null || request.FromDate <= old.EffectiveTo, "fromDate", "Ngày áp dụng nằm sau khi lịch cũ đã kết thúc.");
        Guard.Check(request.Schedule.RouteId == old.RouteId, "schedule.routeId", "Lịch thay thế phải cùng tuyến.");
        request.Schedule.EffectiveFrom = request.FromDate;
        var next = new Schedule { OrganizationId = organizationId, ReplacesScheduleId = old.Id };
        var reserved = await ApplyAsync(next, request.Schedule, ct);
        old.EffectiveTo = request.FromDate.AddDays(-1);
        old.Version++;
        var activate = old.Status == ScheduleStatuses.Active;
        if (activate)
        {
            await RequireRunnableAsync(next, true, ct);
            await RequireNoOverlapAsync(next, old.Id, ct);
            next.Status = ScheduleStatuses.Active;
        }
        db.Schedules.Add(next);
        AddSeats(next.Id, reserved);
        await Guard.SaveAsync(db, ct);
        return (await MapAsync(organizationId, [next], ct))[0];
    }

    private async Task<List<string>> ApplyAsync(Schedule schedule, SaveScheduleRequest r, CancellationToken ct)
    {
        var route = await db.Routes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.RouteId && x.OrganizationId == schedule.OrganizationId, ct)
            ?? throw new NotFoundException("Không tìm thấy tuyến.");
        var revisionId = r.RouteRevisionId ?? route.CurrentRevisionId
            ?? throw new ConflictException("Tuyến chưa có bản công bố.");
        var revision = await db.Revisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == revisionId && x.RouteId == route.Id, ct)
            ?? throw new NotFoundException("Không tìm thấy bản tuyến.");
        Guard.Check(revision.Status != RevisionStatuses.Draft, "routeRevisionId", "Chỉ dùng bản tuyến đã công bố.");
        Guard.Check(TimeOnly.TryParseExact(r.DepartureTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time),
            "departureTime", "Giờ xuất bến dạng HH:mm.");
        Guard.Check(r.DaysOfWeek.Count > 0 && r.DaysOfWeek.All(x => x is >= 1 and <= 7), "daysOfWeek", "Chọn ít nhất một ngày trong tuần.");
        Guard.Check(r.EffectiveFrom != default, "effectiveFrom", "Chọn ngày bắt đầu.");
        Guard.Check(r.EffectiveTo is null || r.EffectiveTo >= r.EffectiveFrom, "effectiveTo", "Ngày kết thúc không trước ngày bắt đầu.");
        Guard.Check(ParticipationModes.All.Contains(r.ParticipationMode), "participationMode", "Chế độ tham gia không hợp lệ.");
        var labels = r.VieGoSeats.Select(x => (x ?? "").Trim().ToUpperInvariant()).Where(x => x.Length > 0).Distinct().ToList();
        Guard.Check(labels.Count == 0 || r.PlannedVehicleId is not null, "vieGoSeats", "Chọn xe dự kiến trước khi dành ghế cho VieGo.");
        if (r.ParticipationMode == ParticipationModes.InstantBooking)
            Guard.Check(r.PlannedVehicleId is not null && labels.Count > 0, "participationMode",
                "Đặt ngay cần xe dự kiến và ít nhất một ghế dành cho VieGo.");
        schedule.RouteId = route.Id; schedule.RouteRevisionId = revision.Id;
        schedule.DepartureMinutes = time.Hour * 60 + time.Minute;
        schedule.DaysOfWeek = r.DaysOfWeek.Distinct().Aggregate(0, (mask, day) => mask | (1 << (day - 1)));
        schedule.EffectiveFrom = r.EffectiveFrom; schedule.EffectiveTo = r.EffectiveTo;
        schedule.PlannedVehicleId = r.PlannedVehicleId; schedule.ParticipationMode = r.ParticipationMode;
        await ValidateSeatsAsync(schedule, labels, ct);
        return labels;
    }

    private async Task ValidateSeatsAsync(Schedule schedule, IReadOnlyCollection<string> labels, CancellationToken ct)
    {
        if (schedule.PlannedVehicleId is not { } vehicleId) return;
        _ = await access.Organizations.FindManagedVehicleAsync(schedule.OrganizationId, vehicleId, ct)
            ?? throw new ValidationAppException(new Dictionary<string, string[]> { ["plannedVehicleId"] = ["Xe không thuộc tổ chức đang quản lý."] });
        var sellable = await seats.SellableLabelsForVehicleAsync(schedule.OrganizationId, vehicleId, ct);
        Guard.Check(sellable is not null, "plannedVehicleId", "Xe chưa gắn sơ đồ ghế.");
        var missing = labels.Where(x => !sellable!.Contains(x)).ToList();
        Guard.Check(missing.Count == 0, "vieGoSeats", "Ghế không có trong sơ đồ của xe: " + string.Join(", ", missing));
    }

    private async Task RequireRunnableAsync(Schedule schedule, bool checkStartDate, CancellationToken ct)
    {
        var route = await db.Routes.AsNoTracking().SingleAsync(x => x.Id == schedule.RouteId, ct);
        if (!await ScheduleRules.RegistrationApprovedAsync(db, route.ServiceRegistrationId, ct))
            throw new ConflictException("Dịch vụ xe khách của tổ chức không còn được duyệt.");
        if (route.Status != RouteStatuses.Active) throw new ConflictException("Tuyến đang ngừng hoạt động.");
        var revision = await db.Revisions.AsNoTracking().SingleAsync(x => x.Id == schedule.RouteRevisionId, ct);
        // Lịch đang chạy được giữ bản cũ; lịch mới chỉ kích hoạt với bản đang công bố.
        if (checkStartDate)
        {
            if (revision.Status != RevisionStatuses.Published) throw new ConflictException("Lịch mới phải dùng bản tuyến đang công bố.");
            Guard.Check(schedule.EffectiveFrom >= access.Today, "effectiveFrom", "Ngày bắt đầu không được trước hôm nay.");
        }
        if (schedule.EffectiveTo is { } end && end < access.Today) throw new ConflictException("Lịch đã hết thời gian áp dụng.");
        // Q-M2-FARE-010A: lịch nhận khách qua VieGo cần bảng giá đã công bố từ ngày lịch bắt đầu chạy.
        if (schedule.ParticipationMode != ParticipationModes.InformationOnly
            && !await FareService.HasPublishedFareAsync(db, schedule.RouteRevisionId, FirstRunDate(schedule), ct))
            throw new ConflictException("Bản tuyến của lịch chưa có bảng giá công bố áp dụng từ ngày lịch chạy. Owner cần công bố bảng giá trước.");
    }

    private DateOnly FirstRunDate(Schedule schedule) => schedule.EffectiveFrom > access.Today ? schedule.EffectiveFrom : access.Today;

    private async Task RequireNoOverlapAsync(Schedule schedule, Guid? ignoreId, CancellationToken ct)
    {
        var others = await db.Schedules.AsNoTracking().Where(x => x.RouteId == schedule.RouteId && x.Id != schedule.Id
            && x.Id != ignoreId && x.Status == ScheduleStatuses.Active && x.DepartureMinutes == schedule.DepartureMinutes).ToListAsync(ct);
        var clash = others.FirstOrDefault(x => (x.DaysOfWeek & schedule.DaysOfWeek) != 0
            && (x.EffectiveTo is null || x.EffectiveTo >= schedule.EffectiveFrom)
            && (schedule.EffectiveTo is null || schedule.EffectiveTo >= x.EffectiveFrom));
        if (clash is not null) throw new ConflictException("Đã có lịch đang chạy cùng tuyến, cùng giờ xuất bến trong cùng ngày.");
    }

    private void AddSeats(Guid scheduleId, IEnumerable<string> labels)
    {
        foreach (var label in labels) db.VieGoSeats.Add(new ScheduleVieGoSeat { ScheduleId = scheduleId, SeatLabel = label });
    }

    private async Task<Schedule> FindAsync(Guid organizationId, Guid id, bool tracked, CancellationToken ct)
    {
        var query = tracked ? db.Schedules : db.Schedules.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct)
            ?? throw new NotFoundException("Không tìm thấy lịch chạy.");
    }

    private async Task<List<ScheduleResponse>> MapAsync(Guid organizationId, IReadOnlyList<Schedule> rows, CancellationToken ct)
    {
        var routeIds = rows.Select(x => x.RouteId).Distinct().ToList();
        var routes = await db.Routes.AsNoTracking().IgnoreQueryFilters().Where(x => routeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var revisionIds = rows.Select(x => x.RouteRevisionId).Distinct().ToList();
        var revisions = await db.Revisions.AsNoTracking().Where(x => revisionIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.RevisionNo, ct);
        var ids = rows.Select(x => x.Id).ToList();
        var reserved = (await db.VieGoSeats.AsNoTracking().Where(x => ids.Contains(x.ScheduleId)).ToListAsync(ct)).ToLookup(x => x.ScheduleId);
        var fareReady = new Dictionary<Guid, bool>();
        foreach (var x in rows.Where(x => x.ParticipationMode != ParticipationModes.InformationOnly))
            fareReady[x.Id] = await FareService.HasPublishedFareAsync(db, x.RouteRevisionId, FirstRunDate(x), ct);
        var vehicles = new Dictionary<Guid, (string Plate, int? Sellable)>();
        foreach (var vehicleId in rows.Where(x => x.PlannedVehicleId != null).Select(x => x.PlannedVehicleId!.Value).Distinct())
        {
            var vehicle = await access.Organizations.FindManagedVehicleAsync(organizationId, vehicleId, ct);
            var labels = await seats.SellableLabelsForVehicleAsync(organizationId, vehicleId, ct);
            vehicles[vehicleId] = (vehicle?.RegistrationPlate ?? "(không còn quản lý)", labels?.Count);
        }
        return rows.Select(x =>
        {
            var route = routes.GetValueOrDefault(x.RouteId);
            var vehicle = x.PlannedVehicleId is { } v && vehicles.TryGetValue(v, out var info) ? info : default;
            return new ScheduleResponse(x.Id, x.RouteId, route?.Code ?? "", route?.Name ?? "", x.RouteRevisionId,
                revisions.GetValueOrDefault(x.RouteRevisionId), $"{x.DepartureMinutes / 60:00}:{x.DepartureMinutes % 60:00}",
                Enumerable.Range(1, 7).Where(d => (x.DaysOfWeek & (1 << (d - 1))) != 0).ToList(), x.EffectiveFrom, x.EffectiveTo,
                x.PlannedVehicleId, vehicle.Plate, vehicle.Sellable, x.ParticipationMode,
                reserved[x.Id].Select(s => s.SeatLabel).OrderBy(s => s).ToList(), x.Status, x.StatusReason, x.ReplacesScheduleId, x.Version,
                fareReady.GetValueOrDefault(x.Id, true));
        }).ToList();
    }
}
