namespace TransConnect.Modules.Transport.Dto;

public sealed class CreateFareRequest
{
    public Guid RouteRevisionId { get; set; }
}

public sealed class FarePriceRequest
{
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public int Price { get; set; }
    public int? VipPrice { get; set; }
}

public sealed class SaveFareRequest
{
    public DateOnly EffectiveFrom { get; set; }
    public int VipSurcharge { get; set; }
    public string? Note { get; set; }
    public List<FarePriceRequest> Prices { get; set; } = [];
    public int Version { get; set; }
}

public sealed record FareStopResponse(Guid SharedLocationId, int Sequence, string Name, bool AllowPickup, bool AllowDropoff);

public sealed record FarePriceResponse(Guid FromLocationId, Guid ToLocationId, int Price, int? VipPrice, int VipTotal);

/// <summary>Phase chỉ có ở bảng Published: Current (áp dụng hôm nay), Upcoming, Past.</summary>
public sealed record FareTableResponse(Guid Id, Guid RouteId, Guid RouteRevisionId, int RevisionNo, int TableNo, string Status,
    string? Phase, DateOnly EffectiveFrom, int VipSurcharge, string? Note, DateTime CreatedAt, DateTime? PublishedAt, int Version,
    int PriceCount, int PairCount, IReadOnlyList<FareStopResponse>? Stops = null, IReadOnlyList<FarePriceResponse>? Prices = null,
    IReadOnlyList<string>? Warnings = null);
