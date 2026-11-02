using TransConnect.Modules.Locations.Entities;

namespace TransConnect.Modules.Locations.Dto.Responses;

public sealed record SharedLocationResponse
{
    public Guid Id { get; init; }
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
    public int Version { get; init; }

    public static SharedLocationResponse From(SharedLocation value) => new()
    {
        Id = value.Id,
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
        Version = value.Version,
    };
}
