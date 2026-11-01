using TransConnect.Modules.Locations.Entities;

namespace TransConnect.Modules.Locations.Dto.Responses;

public sealed record SharedLocationVersionResponse
{
    public Guid LocationId { get; init; }
    public int Version { get; init; }
    public Guid DatasetId { get; init; }
    public string ProvinceCode { get; init; } = "";
    public string WardCode { get; init; } = "";
    public string AddressDetail { get; init; } = "";
    public string? AccessInstructions { get; init; }
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public string? ConfirmationMethod { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    public Guid? ConfirmedBy { get; init; }
    public bool IsActive { get; init; }
    public Guid ChangedBy { get; init; }
    public DateTime ChangedAt { get; init; }
    public string Reason { get; init; } = "";

    public static SharedLocationVersionResponse From(SharedLocationVersion value) => new()
    {
        LocationId = value.LocationId,
        Version = value.Version,
        DatasetId = value.DatasetId,
        ProvinceCode = value.ProvinceCode,
        WardCode = value.WardCode,
        AddressDetail = value.AddressDetail,
        AccessInstructions = value.AccessInstructions,
        Latitude = value.Latitude,
        Longitude = value.Longitude,
        Name = value.Name,
        Type = value.Type,
        ConfirmationMethod = value.ConfirmationMethod,
        ConfirmedAt = value.ConfirmedAt,
        ConfirmedBy = value.ConfirmedBy,
        IsActive = value.IsActive,
        ChangedBy = value.ChangedBy,
        ChangedAt = value.ChangedAt,
        Reason = value.Reason,
    };
}
