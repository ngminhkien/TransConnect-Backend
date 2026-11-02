namespace TransConnect.Modules.Locations.Entities;

public sealed class LocationChangeRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid SubmittedBy { get; set; }
    public string Kind { get; set; } = "";
    public Guid? LocationId { get; set; }
    public int? BaseVersion { get; set; }
    public Guid DatasetId { get; set; }
    public string ProvinceCode { get; set; } = "";
    public string WardCode { get; set; } = "";
    public string AddressDetail { get; set; } = "";
    public string? AccessInstructions { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string? ConfirmationMethod { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public int Version { get; set; } = 1;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewReason { get; set; }
}
