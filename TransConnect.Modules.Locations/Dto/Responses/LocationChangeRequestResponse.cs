using TransConnect.Modules.Locations.Entities;

namespace TransConnect.Modules.Locations.Dto.Responses;

public sealed record LocationChangeRequestResponse
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public Guid SubmittedBy { get; init; }
    public string Kind { get; init; } = "";
    public Guid? LocationId { get; init; }
    public int? BaseVersion { get; init; }
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
    public string Reason { get; init; } = "";
    public string Status { get; init; } = "";
    public int Version { get; init; }
    public DateTime SubmittedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public Guid? ReviewedBy { get; init; }
    public string? ReviewReason { get; init; }

    public static LocationChangeRequestResponse From(LocationChangeRequest value) => new()
    {
        Id = value.Id,
        OrganizationId = value.OrganizationId,
        SubmittedBy = value.SubmittedBy,
        Kind = value.Kind,
        LocationId = value.LocationId,
        BaseVersion = value.BaseVersion,
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
        Reason = value.Reason,
        Status = value.Status,
        Version = value.Version,
        SubmittedAt = value.SubmittedAt,
        ReviewedAt = value.ReviewedAt,
        ReviewedBy = value.ReviewedBy,
        ReviewReason = value.ReviewReason,
    };
}
