namespace TransConnect.Modules.Locations.Entities;

public sealed class SharedLocationVersion
{
    public Guid LocationId { get; set; }
    public int Version { get; set; }
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
    public bool IsActive { get; set; }
    public Guid ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = "";
}
