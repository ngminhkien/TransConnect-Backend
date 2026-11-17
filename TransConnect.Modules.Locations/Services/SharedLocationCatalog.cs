using Microsoft.EntityFrameworkCore;
using TransConnect.Common.Transport;
using TransConnect.Modules.Locations.Persistence;

namespace TransConnect.Modules.Locations.Services;

public sealed class SharedLocationCatalog(LocationsDbContext db) : ISharedLocationCatalog
{
    public async Task<IReadOnlyDictionary<Guid, SharedLocationSnapshot>> GetActiveAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return new Dictionary<Guid, SharedLocationSnapshot>();
        var rows = await db.SharedLocations.AsNoTracking().Where(x => ids.Contains(x.Id) && x.IsActive).ToListAsync(ct);
        return rows.ToDictionary(x => x.Id, x => new SharedLocationSnapshot(x.Id, x.Version, x.Name, x.Type, x.AddressDetail,
            x.ProvinceCode, x.WardCode, x.Latitude, x.Longitude));
    }
}
