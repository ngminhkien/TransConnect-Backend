using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Transport.Dto;
using TransConnect.Modules.Transport.Entities;
using TransConnect.Modules.Transport.Persistence;

namespace TransConnect.Modules.Transport.Services;

public sealed class SeatLayoutService(TransportDbContext db, TransportAccess access)
{
    public const int MaxRows = 20, MaxColumns = 6;

    public async Task<IReadOnlyList<SeatLayoutResponse>> TemplatesAsync(CancellationToken ct)
    {
        await access.ActorAsync(ct);
        var layouts = await db.SeatLayouts.AsNoTracking().Where(x => x.OrganizationId == null && x.Status != SeatLayoutStatuses.Archived)
            .OrderBy(x => x.Name).ToListAsync(ct);
        return await MapAsync(layouts, ct);
    }

    public async Task<IReadOnlyList<SeatLayoutResponse>> ListAsync(Guid organizationId, CancellationToken ct)
    {
        await access.ReaderAsync(organizationId, ct);
        var layouts = await db.SeatLayouts.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.Status != SeatLayoutStatuses.Archived)
            .OrderBy(x => x.Name).ToListAsync(ct);
        return await MapAsync(layouts, ct);
    }

    public async Task<SeatLayoutResponse> GetAsync(Guid organizationId, Guid id, CancellationToken ct)
    {
        await access.ReaderAsync(organizationId, ct);
        return (await MapAsync([await FindAsync(organizationId, id, false, ct)], ct))[0];
    }

    public async Task<SeatLayoutResponse> CreateAsync(Guid organizationId, SaveSeatLayoutRequest request, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var layout = new SeatLayout { OrganizationId = organizationId };
        if (request.SourceLayoutId is { } sourceId)
        {
            var source = await db.SeatLayouts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sourceId
                && (x.OrganizationId == null || x.OrganizationId == organizationId), ct)
                ?? throw new NotFoundException("Không tìm thấy sơ đồ nguồn.");
            layout.Name = string.IsNullOrWhiteSpace(request.Name) ? source.Name : Guard.Required(request.Name, 200, "name");
            layout.LayoutKind = source.LayoutKind; layout.DeckCount = source.DeckCount; layout.SourceLayoutId = source.Id;
            db.SeatLayouts.Add(layout);
            foreach (var deck in await db.SeatDecks.AsNoTracking().Where(x => x.LayoutId == sourceId).ToListAsync(ct))
                db.SeatDecks.Add(new SeatLayoutDeck { LayoutId = layout.Id, DeckNo = deck.DeckNo, Rows = deck.Rows, Columns = deck.Columns });
            foreach (var cell in await db.SeatCells.AsNoTracking().Where(x => x.LayoutId == sourceId).ToListAsync(ct))
            {
                cell.Id = Guid.NewGuid(); cell.LayoutId = layout.Id;
                db.SeatCells.Add(cell);
            }
        }
        else
        {
            db.SeatLayouts.Add(layout);
            Apply(layout, request);
        }
        await Guard.SaveAsync(db, ct);
        return (await MapAsync([layout], ct))[0];
    }

    public async Task<SeatLayoutResponse> UpdateAsync(Guid organizationId, Guid id, SaveSeatLayoutRequest request, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var layout = await FindAsync(organizationId, id, true, ct);
        Guard.Version(request.Version, layout.Version);
        if (layout.Status != SeatLayoutStatuses.Draft)
            throw new ConflictException("Sơ đồ đã gắn vào xe nên đã khóa; hãy sao chép thành sơ đồ mới để sửa.");
        db.SeatDecks.RemoveRange(await db.SeatDecks.Where(x => x.LayoutId == id).ToListAsync(ct));
        db.SeatCells.RemoveRange(await db.SeatCells.Where(x => x.LayoutId == id).ToListAsync(ct));
        Apply(layout, request);
        layout.Version++;
        await Guard.SaveAsync(db, ct);
        return (await MapAsync([layout], ct))[0];
    }

    public async Task ArchiveAsync(Guid organizationId, Guid id, int version, CancellationToken ct)
    {
        await access.MemberAsync(organizationId, ct);
        var layout = await FindAsync(organizationId, id, true, ct);
        Guard.Version(version, layout.Version);
        if (await db.VehicleLayouts.AnyAsync(x => x.LayoutId == id && x.EndedAt == null, ct))
            throw new ConflictException("Sơ đồ đang gắn với xe, gỡ hoặc thay sơ đồ của xe trước.");
        layout.Status = SeatLayoutStatuses.Archived; layout.Version++;
        await Guard.SaveAsync(db, ct);
    }

    public async Task<IReadOnlyList<VehicleSeatLayoutResponse>> VehicleLayoutsAsync(Guid organizationId, CancellationToken ct)
    {
        await access.ReaderAsync(organizationId, ct);
        var links = await db.VehicleLayouts.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.EndedAt == null).ToListAsync(ct);
        var layoutIds = links.Select(x => x.LayoutId).Distinct().ToList();
        var names = await db.SeatLayouts.AsNoTracking().Where(x => layoutIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var sellable = await SellableCountsAsync(layoutIds, ct);
        return links.Select(x => new VehicleSeatLayoutResponse(x.VehicleId, x.LayoutId, names.GetValueOrDefault(x.LayoutId) ?? "",
            sellable.GetValueOrDefault(x.LayoutId), x.AttachedAt)).ToList();
    }

    public async Task<SeatLayoutResponse?> VehicleLayoutAsync(Guid organizationId, Guid vehicleId, CancellationToken ct)
    {
        await access.ReaderAsync(organizationId, ct);
        var link = await db.VehicleLayouts.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId
            && x.VehicleId == vehicleId && x.EndedAt == null, ct);
        if (link is null) return null;
        var layout = await db.SeatLayouts.AsNoTracking().SingleAsync(x => x.Id == link.LayoutId, ct);
        return (await MapAsync([layout], ct))[0];
    }

    public async Task<SeatLayoutResponse> AttachAsync(Guid organizationId, Guid vehicleId, AttachSeatLayoutRequest request, CancellationToken ct)
    {
        var member = await access.MemberAsync(organizationId, ct);
        var vehicle = await access.Organizations.FindManagedVehicleAsync(organizationId, vehicleId, ct)
            ?? throw new NotFoundException("Không tìm thấy xe tổ chức đang quản lý.");
        var layout = await FindAsync(organizationId, request.LayoutId, true, ct);
        if (layout.Status == SeatLayoutStatuses.Archived) throw new ConflictException("Sơ đồ đã lưu trữ.");
        var cells = await db.SeatCells.AsNoTracking().Where(x => x.LayoutId == layout.Id).ToListAsync(ct);
        var sellable = cells.Where(x => x.CellType == SeatCellTypes.Seat && x.IsSellable).Select(x => x.Label!).ToHashSet();
        if (vehicle.SeatCount is { } seatCount)
            Guard.Check(sellable.Count <= seatCount, "layoutId",
                $"Sơ đồ có {sellable.Count} ghế bán, vượt số chỗ theo đăng ký xe ({seatCount}).");
        var current = await db.VehicleLayouts.SingleOrDefaultAsync(x => x.OrganizationId == organizationId
            && x.VehicleId == vehicleId && x.EndedAt == null, ct);
        if (current?.LayoutId == layout.Id) return (await MapAsync([layout], ct))[0];
        // Không làm mất ghế VieGo của lịch đang chạy với xe này (Q-M2-SERVICE-005c).
        var schedules = await db.Schedules.AsNoTracking().Where(x => x.OrganizationId == organizationId && x.PlannedVehicleId == vehicleId
            && (x.Status == ScheduleStatuses.Active || x.Status == ScheduleStatuses.Paused)).Select(x => x.Id).ToListAsync(ct);
        var reserved = await db.VieGoSeats.AsNoTracking().Where(x => schedules.Contains(x.ScheduleId)).ToListAsync(ct);
        var broken = reserved.Where(x => !sellable.Contains(x.SeatLabel)).Select(x => x.ScheduleId).Distinct().ToList();
        if (broken.Count > 0)
            throw new ConflictException($"Sơ đồ mới thiếu ghế dành cho VieGo của {broken.Count} lịch đang dùng xe này. Hãy thay lịch trước: "
                + string.Join(", ", broken));
        if (current is not null) current.EndedAt = DateTime.UtcNow;
        db.VehicleLayouts.Add(new VehicleSeatLayout { OrganizationId = organizationId, VehicleId = vehicleId, LayoutId = layout.Id, AttachedBy = member.AccountId });
        if (layout.Status == SeatLayoutStatuses.Draft) { layout.Status = SeatLayoutStatuses.Locked; layout.Version++; }
        await Guard.SaveAsync(db, ct);
        return (await MapAsync([layout], ct))[0];
    }

    /// <summary>Mã ghế bán được của sơ đồ đang gắn với xe, hoặc null nếu xe chưa có sơ đồ.</summary>
    internal async Task<HashSet<string>?> SellableLabelsForVehicleAsync(Guid organizationId, Guid vehicleId, CancellationToken ct)
    {
        var link = await db.VehicleLayouts.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId
            && x.VehicleId == vehicleId && x.EndedAt == null, ct);
        if (link is null) return null;
        return (await db.SeatCells.AsNoTracking().Where(x => x.LayoutId == link.LayoutId && x.CellType == SeatCellTypes.Seat && x.IsSellable)
            .Select(x => x.Label!).ToListAsync(ct)).ToHashSet();
    }

    private void Apply(SeatLayout layout, SaveSeatLayoutRequest request)
    {
        layout.Name = Guard.Required(request.Name, 200, "name");
        Guard.Check(SeatLayoutKinds.All.Contains(request.LayoutKind), "layoutKind", "Loại sơ đồ không hợp lệ.");
        layout.LayoutKind = request.LayoutKind;
        Validate(request.Decks, request.Cells);
        layout.DeckCount = request.Decks.Count;
        foreach (var d in request.Decks)
            db.SeatDecks.Add(new SeatLayoutDeck { LayoutId = layout.Id, DeckNo = d.DeckNo, Rows = d.Rows, Columns = d.Columns });
        foreach (var c in request.Cells.Where(x => x.CellType != SeatCellTypes.Empty))
        {
            var seat = c.CellType == SeatCellTypes.Seat;
            db.SeatCells.Add(new SeatLayoutCell
            {
                LayoutId = layout.Id, DeckNo = c.DeckNo, Row = c.Row, Col = c.Col, CellType = c.CellType,
                Label = seat ? c.Label!.Trim().ToUpperInvariant() : null,
                SeatClass = seat ? (string.IsNullOrWhiteSpace(c.SeatClass) ? SeatClasses.Standard : c.SeatClass) : null,
                IsSellable = seat && c.IsSellable
            });
        }
    }

    internal static void Validate(IReadOnlyList<DeckRequest> decks, IReadOnlyList<CellRequest> cells)
    {
        Guard.Check(decks.Count is 1 or 2, "decks", "Sơ đồ có 1 hoặc 2 tầng.");
        Guard.Check(decks.Select(x => x.DeckNo).OrderBy(x => x).SequenceEqual(Enumerable.Range(1, decks.Count)), "decks",
            "Tầng đánh số từ 1.");
        foreach (var d in decks)
            Guard.Check(d.Rows is >= 1 and <= MaxRows && d.Columns is >= 1 and <= MaxColumns, $"decks[{d.DeckNo}]",
                $"Mỗi tầng tối đa {MaxRows} hàng × {MaxColumns} cột.");
        var size = decks.ToDictionary(x => x.DeckNo);
        var positions = new HashSet<(int, int, int)>();
        var labels = new HashSet<string>();
        for (var i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            Guard.Check(SeatCellTypes.All.Contains(c.CellType), $"cells[{i}].cellType", "Loại ô không hợp lệ.");
            Guard.Check(size.TryGetValue(c.DeckNo, out var d) && c.Row >= 1 && c.Row <= d.Rows && c.Col >= 1 && c.Col <= d.Columns,
                $"cells[{i}]", "Ô nằm ngoài lưới của tầng.");
            Guard.Check(positions.Add((c.DeckNo, c.Row, c.Col)), $"cells[{i}]", "Hai ô trùng vị trí.");
            if (c.CellType != SeatCellTypes.Seat) continue;
            var label = (c.Label ?? "").Trim().ToUpperInvariant();
            Guard.Check(Regex.IsMatch(label, @"\A[A-Z0-9][A-Z0-9\-]{0,9}\z"), $"cells[{i}].label", "Mã ghế 1–10 ký tự chữ/số.");
            Guard.Check(labels.Add(label), $"cells[{i}].label", $"Mã ghế {label} bị trùng.");
            Guard.Check(c.SeatClass is null || SeatClasses.All.Contains(c.SeatClass), $"cells[{i}].seatClass", "Hạng ghế không hợp lệ.");
        }
        Guard.Check(cells.Any(x => x.CellType == SeatCellTypes.Seat && x.IsSellable), "cells", "Sơ đồ cần ít nhất một ghế bán.");
    }

    private async Task<SeatLayout> FindAsync(Guid organizationId, Guid id, bool tracked, CancellationToken ct)
    {
        var query = tracked ? db.SeatLayouts : db.SeatLayouts.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct)
            ?? throw new NotFoundException("Không tìm thấy sơ đồ ghế.");
    }

    private async Task<Dictionary<Guid, int>> SellableCountsAsync(IReadOnlyCollection<Guid> layoutIds, CancellationToken ct)
        => await db.SeatCells.AsNoTracking().Where(x => layoutIds.Contains(x.LayoutId) && x.CellType == SeatCellTypes.Seat && x.IsSellable)
            .GroupBy(x => x.LayoutId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);

    private async Task<List<SeatLayoutResponse>> MapAsync(IReadOnlyList<SeatLayout> layouts, CancellationToken ct)
    {
        var ids = layouts.Select(x => x.Id).ToList();
        var decks = (await db.SeatDecks.AsNoTracking().Where(x => ids.Contains(x.LayoutId)).ToListAsync(ct)).ToLookup(x => x.LayoutId);
        var cells = (await db.SeatCells.AsNoTracking().Where(x => ids.Contains(x.LayoutId)).ToListAsync(ct)).ToLookup(x => x.LayoutId);
        var vehicles = (await db.VehicleLayouts.AsNoTracking().Where(x => ids.Contains(x.LayoutId) && x.EndedAt == null).ToListAsync(ct))
            .ToLookup(x => x.LayoutId);
        return layouts.Select(l =>
        {
            var c = cells[l.Id].OrderBy(x => x.DeckNo).ThenBy(x => x.Row).ThenBy(x => x.Col).ToList();
            return new SeatLayoutResponse(l.Id, l.OrganizationId, l.Name, l.LayoutKind, l.DeckCount, l.Status, l.SourceLayoutId, l.Version,
                c.Count(x => x.CellType == SeatCellTypes.Seat), c.Count(x => x.CellType == SeatCellTypes.Seat && x.IsSellable),
                decks[l.Id].OrderBy(x => x.DeckNo).Select(x => new DeckResponse(x.DeckNo, x.Rows, x.Columns)).ToList(),
                c.Select(x => new CellResponse(x.DeckNo, x.Row, x.Col, x.CellType, x.Label, x.SeatClass, x.IsSellable)).ToList(),
                vehicles[l.Id].Select(x => x.VehicleId).ToList());
        }).ToList();
    }
}
