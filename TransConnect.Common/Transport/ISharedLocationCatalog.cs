namespace TransConnect.Common.Transport;

public sealed record SharedLocationSnapshot(Guid Id, int Version, string Name, string Type, string AddressDetail,
    string ProvinceCode, string WardCode, decimal? Latitude, decimal? Longitude);

/// <summary>Locations-owned read of active public VieGo points (id + version + snapshot data).</summary>
public interface ISharedLocationCatalog
{
    Task<IReadOnlyDictionary<Guid, SharedLocationSnapshot>> GetActiveAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
}
