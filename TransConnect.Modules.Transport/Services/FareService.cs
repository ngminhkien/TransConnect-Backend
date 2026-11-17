using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Transport.Dto;
using TransConnect.Modules.Transport.Entities;
using TransConnect.Modules.Transport.Persistence;

namespace TransConnect.Modules.Transport.Services;

/// <summary>Bảng giá theo chặng (Q-M2-FARE). Chỉ Owner ghi (007C); thành viên và Admin services.review chỉ xem.</summary>
public sealed class FareService(TransportDbContext db, TransportAccess access)
{
    public const int MaxPrice = 50_000_000;
    private const int MaxWarnings = 20;

    public async Task<IReadOnlyList<FareTableResponse>> ListAsync(Guid organizationId, Guid routeId, CancellationToken ct)
    {
        await access.ReaderAsync(organizationId, ct);
        await RouteAsync(organizationId, routeId, ct);
        var tables = await db.FareTables.AsNoTracking().Where(x => x.RouteId == routeId).ToListAsync(ct);
        var revisions = await RevisionNosAsync(routeId, ct);
        var ids = tables.Select(x => x.Id).ToList();
        var counts = await db.FarePrices.AsNoTracking().Where(x => ids.Contains(x.FareTableId)).GroupBy(x => x.FareTableId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var stops = await StopsByRevisionAsync(tables.Select(x => x.RouteRevisionId).Distinct().ToList(), ct);
        return tables
            .OrderByDescending(x => revisions.GetValueOrDefault(x.RouteRevisionId)).ThenByDescending(x => x.TableNo)
            .Select(x => Map(x, revisions.GetValueOrDefault(x.RouteRevisionId), tables, counts.GetValueOrDefault(x.Id),
                Pairs(stops.GetValueOrDefault(x.RouteRevisionId) ?? []).Count))
            .ToList();
    }

    public async Task<FareTableResponse> GetAsync(Guid organizationId, Guid routeId, Guid id, CancellationToken ct)
    {
        await access.ReaderAsync(organizationId, ct);
        await RouteAsync(organizationId, routeId, ct);
        return await DetailAsync(await FindAsync(routeId, id, false, ct), ct);
    }

    /// <summary>Tạo bảng nháp cho một bản tuyến; sao chép giá các cặp còn tồn tại (001A).</summary>
    public async Task<FareTableResponse> CreateAsync(Guid organizationId, Guid routeId, CreateFareRequest request, CancellationToken ct)
    {
        var owner = await access.OwnerAsync(organizationId, ct);
        var route = await RouteAsync(organizationId, routeId, ct);
        await RequireWritableAsync(route, ct);
        var revision = await db.Revisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.RouteRevisionId && x.RouteId == routeId, ct)
            ?? throw new NotFoundException("Không tìm thấy bản tuyến.");
        Guard.Check(revision.Status != RevisionStatuses.Draft, "routeRevisionId", "Chỉ lập bảng giá cho bản tuyến đã công bố.");
        if (await db.FareTables.AnyAsync(x => x.RouteRevisionId == revision.Id && x.Status == FareStatuses.Draft, ct))
            throw new ConflictException("Bản tuyến đã có bảng giá nháp đang soạn.");
        var stops = await StopsAsync(revision.Id, ct);
        var pairs = Pairs(stops).ToHashSet();
        var source = await CopySourceAsync(routeId, revision, ct);
        var next = (await db.FareTables.Where(x => x.RouteRevisionId == revision.Id).MaxAsync(x => (int?)x.TableNo, ct) ?? 0) + 1;
        var table = new FareTable
        {
            OrganizationId = organizationId, RouteId = routeId, RouteRevisionId = revision.Id, TableNo = next,
            EffectiveFrom = access.Today, VipSurcharge = source?.VipSurcharge ?? 0, CreatedBy = owner.AccountId
        };
        db.FareTables.Add(table);
        if (source is not null)
        {
            var prices = await db.FarePrices.AsNoTracking().Where(x => x.FareTableId == source.Id).ToListAsync(ct);
            foreach (var p in prices.Where(p => pairs.Contains((p.FromLocationId, p.ToLocationId))))
                db.FarePrices.Add(new FarePrice
                {
                    FareTableId = table.Id, FromLocationId = p.FromLocationId, ToLocationId = p.ToLocationId,
                    Price = p.Price, VipPrice = p.VipPrice
                });
        }
        await Guard.SaveAsync(db, ct);
        return await DetailAsync(table, ct);
    }

    public async Task<FareTableResponse> SaveAsync(Guid organizationId, Guid routeId, Guid id, SaveFareRequest request, CancellationToken ct)
    {
        await access.OwnerAsync(organizationId, ct);
        var route = await RouteAsync(organizationId, routeId, ct);
        await RequireWritableAsync(route, ct);
        var table = await DraftAsync(routeId, id, ct);
        Guard.Version(request.Version, table.Version);
        Guard.Check(request.EffectiveFrom != default, "effectiveFrom", "Chọn ngày áp dụng.");
        Guard.Check(ValidAmount(request.VipSurcharge, allowZero: true), "vipSurcharge",
            "Phụ thu Vip là số VNĐ không âm, bội số 1.000 đ.");
        var pairs = Pairs(await StopsAsync(table.RouteRevisionId, ct)).ToHashSet();
        var seen = new HashSet<(Guid, Guid)>();
        for (var i = 0; i < request.Prices.Count; i++)
        {
            var p = request.Prices[i];
            var pair = (p.FromLocationId, p.ToLocationId);
            Guard.Check(pairs.Contains(pair), $"prices[{i}]", "Cặp điểm không thuộc bản tuyến hoặc không cho đón/trả theo chiều này.");
            Guard.Check(seen.Add(pair), $"prices[{i}]", "Cặp điểm bị lặp.");
            Guard.Check(ValidAmount(p.Price, allowZero: false), $"prices[{i}].price", "Giá là số VNĐ lớn hơn 0, bội số 1.000 đ.");
            Guard.Check(p.VipPrice is null || ValidAmount(p.VipPrice.Value, allowZero: false), $"prices[{i}].vipPrice",
                "Giá Vip là số VNĐ lớn hơn 0, bội số 1.000 đ.");
        }
        db.FarePrices.RemoveRange(await db.FarePrices.Where(x => x.FareTableId == table.Id).ToListAsync(ct));
        foreach (var p in request.Prices)
            db.FarePrices.Add(new FarePrice
            {
                FareTableId = table.Id, FromLocationId = p.FromLocationId, ToLocationId = p.ToLocationId, Price = p.Price, VipPrice = p.VipPrice
            });
        table.EffectiveFrom = request.EffectiveFrom;
        table.VipSurcharge = request.VipSurcharge;
        table.Note = Guard.Optional(request.Note, 1000, "note");
        table.Version++;
        await Guard.SaveAsync(db, ct);
        return await DetailAsync(table, ct);
    }

    public async Task<FareTableResponse> PublishAsync(Guid organizationId, Guid routeId, Guid id, VersionRequest request, CancellationToken ct)
    {
        var owner = await access.OwnerAsync(organizationId, ct);
        var route = await RouteAsync(organizationId, routeId, ct);
        await RequireWritableAsync(route, ct);
        var table = await DraftAsync(routeId, id, ct);
        Guard.Version(request.Version, table.Version);
        Guard.Check(table.EffectiveFrom >= access.Today, "effectiveFrom", "Ngày áp dụng không được trước hôm nay.");
        var stops = await StopsAsync(table.RouteRevisionId, ct);
        var prices = await db.FarePrices.AsNoTracking().Where(x => x.FareTableId == table.Id).ToListAsync(ct);
        // 003A: cặp trống không bán, nhưng phải có giá điểm đầu → điểm cuối.
        Guard.Check(stops.Count >= 2 && prices.Any(x => x.FromLocationId == stops[0].SharedLocationId
            && x.ToLocationId == stops[^1].SharedLocationId), "prices",
            "Cần nhập giá từ điểm đầu đến điểm cuối của tuyến trước khi công bố.");
        // Bảng công bố sau thay các bảng cùng bản tuyến có ngày áp dụng từ ngày này trở đi.
        var replaced = await db.FareTables.Where(x => x.RouteRevisionId == table.RouteRevisionId && x.Id != table.Id
            && x.Status == FareStatuses.Published && x.EffectiveFrom >= table.EffectiveFrom).ToListAsync(ct);
        foreach (var old in replaced) { old.Status = FareStatuses.Superseded; old.Version++; }
        table.Status = FareStatuses.Published; table.PublishedAt = DateTime.UtcNow; table.PublishedBy = owner.AccountId;
        table.Version++;
        await Guard.SaveAsync(db, ct);
        return await DetailAsync(table, ct);
    }

    public async Task DeleteDraftAsync(Guid organizationId, Guid routeId, Guid id, int version, CancellationToken ct)
    {
        await access.OwnerAsync(organizationId, ct);
        await RouteAsync(organizationId, routeId, ct);
        var table = await DraftAsync(routeId, id, ct);
        Guard.Version(version, table.Version);
        db.FarePrices.RemoveRange(await db.FarePrices.Where(x => x.FareTableId == table.Id).ToListAsync(ct));
        db.FareTables.Remove(table);
        await Guard.SaveAsync(db, ct);
    }

    /// <summary>010A: có bảng giá đã công bố của bản tuyến áp dụng từ ngày <paramref name="date"/>.</summary>
    internal static Task<bool> HasPublishedFareAsync(TransportDbContext db, Guid revisionId, DateOnly date, CancellationToken ct)
        => db.FareTables.AnyAsync(x => x.RouteRevisionId == revisionId && x.Status == FareStatuses.Published && x.EffectiveFrom <= date, ct);

    /// <summary>Các cặp được định giá theo 002A: i &lt; j, điểm i cho đón, điểm j cho trả.</summary>
    internal static List<(Guid From, Guid To)> Pairs(IReadOnlyList<RouteRevisionStop> stops)
    {
        var result = new List<(Guid, Guid)>();
        for (var i = 0; i < stops.Count; i++)
        {
            if (!stops[i].AllowPickup) continue;
            for (var j = i + 1; j < stops.Count; j++)
                if (stops[j].AllowDropoff) result.Add((stops[i].SharedLocationId, stops[j].SharedLocationId));
        }
        return result;
    }

    /// <summary>009A: chặng dài rẻ hơn một chặng con nằm trong nó chỉ cảnh báo, không chặn.</summary>
    internal static List<string> Warnings(IReadOnlyList<RouteRevisionStop> stops, IReadOnlyList<FarePrice> prices)
    {
        var index = stops.Select((s, i) => (s.SharedLocationId, i)).ToDictionary(x => x.SharedLocationId, x => x.i);
        var priced = prices.Where(p => index.ContainsKey(p.FromLocationId) && index.ContainsKey(p.ToLocationId))
            .Select(p => (From: index[p.FromLocationId], To: index[p.ToLocationId], p.Price)).ToList();
        var warnings = new List<string>();
        foreach (var outer in priced.OrderBy(x => x.From).ThenByDescending(x => x.To))
        {
            var dearer = priced.Where(x => x != outer && x.From >= outer.From && x.To <= outer.To && x.Price > outer.Price).ToList();
            if (dearer.Count == 0) continue;
            var inner = dearer.MaxBy(x => x.Price);
            warnings.Add($"{stops[outer.From].Name} → {stops[outer.To].Name} ({outer.Price:N0} đ) rẻ hơn chặng con "
                + $"{stops[inner.From].Name} → {stops[inner.To].Name} ({inner.Price:N0} đ).");
            if (warnings.Count == MaxWarnings) break;
        }
        return warnings;
    }

    private static bool ValidAmount(int value, bool allowZero)
        => (allowZero ? value >= 0 : value > 0) && value <= MaxPrice && value % 1000 == 0;

    // Nguồn sao chép: bảng công bố mới nhất của cùng bản tuyến, nếu chưa có thì của bản tuyến công bố gần nhất trước đó.
    private async Task<FareTable?> CopySourceAsync(Guid routeId, RouteRevision revision, CancellationToken ct)
    {
        var published = await db.FareTables.AsNoTracking().Where(x => x.RouteId == routeId && x.Status == FareStatuses.Published)
            .ToListAsync(ct);
        if (published.Count == 0) return null;
        var revisions = await RevisionNosAsync(routeId, ct);
        return published
            .Where(x => revisions.GetValueOrDefault(x.RouteRevisionId) <= revision.RevisionNo)
            .OrderByDescending(x => revisions.GetValueOrDefault(x.RouteRevisionId))
            .ThenByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.TableNo)
            .FirstOrDefault();
    }

    private async Task RequireWritableAsync(TransportRoute route, CancellationToken ct)
    {
        if (!await ScheduleRules.RegistrationApprovedAsync(db, route.ServiceRegistrationId, ct))
            throw new ConflictException("Dịch vụ của tuyến không còn được duyệt; bảng giá chỉ xem được.");
    }

    private async Task<TransportRoute> RouteAsync(Guid organizationId, Guid routeId, CancellationToken ct)
        => await db.Routes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == routeId && x.OrganizationId == organizationId, ct)
           ?? throw new NotFoundException("Không tìm thấy tuyến.");

    private async Task<FareTable> FindAsync(Guid routeId, Guid id, bool tracked, CancellationToken ct)
    {
        var query = tracked ? db.FareTables : db.FareTables.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == id && x.RouteId == routeId, ct)
            ?? throw new NotFoundException("Không tìm thấy bảng giá.");
    }

    private async Task<FareTable> DraftAsync(Guid routeId, Guid id, CancellationToken ct)
    {
        var table = await FindAsync(routeId, id, true, ct);
        if (table.Status != FareStatuses.Draft) throw new ConflictException("Bảng giá đã công bố không sửa được; hãy tạo bảng giá mới.");
        return table;
    }

    private Task<Dictionary<Guid, int>> RevisionNosAsync(Guid routeId, CancellationToken ct)
        => db.Revisions.AsNoTracking().Where(x => x.RouteId == routeId).ToDictionaryAsync(x => x.Id, x => x.RevisionNo, ct);

    private Task<List<RouteRevisionStop>> StopsAsync(Guid revisionId, CancellationToken ct)
        => db.Stops.AsNoTracking().Where(x => x.RevisionId == revisionId).OrderBy(x => x.Sequence).ToListAsync(ct);

    private async Task<Dictionary<Guid, List<RouteRevisionStop>>> StopsByRevisionAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        => (await db.Stops.AsNoTracking().Where(x => ids.Contains(x.RevisionId)).ToListAsync(ct))
            .GroupBy(x => x.RevisionId).ToDictionary(g => g.Key, g => g.OrderBy(x => x.Sequence).ToList());

    private FareTableResponse Map(FareTable x, int revisionNo, IReadOnlyList<FareTable> siblings, int priceCount, int pairCount,
        IReadOnlyList<FareStopResponse>? stops = null, IReadOnlyList<FarePriceResponse>? prices = null, IReadOnlyList<string>? warnings = null)
        => new(x.Id, x.RouteId, x.RouteRevisionId, revisionNo, x.TableNo, x.Status, Phase(x, siblings), x.EffectiveFrom, x.VipSurcharge,
            x.Note, x.CreatedAt, x.PublishedAt, x.Version, priceCount, pairCount, stops, prices, warnings);

    private string? Phase(FareTable table, IReadOnlyList<FareTable> siblings)
    {
        if (table.Status != FareStatuses.Published) return null;
        var today = access.Today;
        if (table.EffectiveFrom > today) return "Upcoming";
        var current = siblings.Where(x => x.RouteRevisionId == table.RouteRevisionId && x.Status == FareStatuses.Published
                && x.EffectiveFrom <= today)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.TableNo).First();
        return current.Id == table.Id ? "Current" : "Past";
    }

    private async Task<FareTableResponse> DetailAsync(FareTable table, CancellationToken ct)
    {
        var stops = await StopsAsync(table.RouteRevisionId, ct);
        var prices = await db.FarePrices.AsNoTracking().Where(x => x.FareTableId == table.Id).ToListAsync(ct);
        var siblings = await db.FareTables.AsNoTracking().Where(x => x.RouteRevisionId == table.RouteRevisionId).ToListAsync(ct);
        var self = siblings.SingleOrDefault(x => x.Id == table.Id) ?? table;
        var revisionNo = await db.Revisions.AsNoTracking().Where(x => x.Id == table.RouteRevisionId).Select(x => x.RevisionNo).SingleAsync(ct);
        var order = stops.Select((s, i) => (s.SharedLocationId, i)).ToDictionary(x => x.SharedLocationId, x => x.i);
        return Map(self, revisionNo, siblings, prices.Count, Pairs(stops).Count,
            stops.Select(s => new FareStopResponse(s.SharedLocationId, s.Sequence, s.Name, s.AllowPickup, s.AllowDropoff)).ToList(),
            prices.OrderBy(p => order.GetValueOrDefault(p.FromLocationId)).ThenBy(p => order.GetValueOrDefault(p.ToLocationId))
                .Select(p => new FarePriceResponse(p.FromLocationId, p.ToLocationId, p.Price, p.VipPrice,
                    p.VipPrice ?? p.Price + table.VipSurcharge)).ToList(),
            Warnings(stops, prices));
    }
}
