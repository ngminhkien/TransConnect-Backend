namespace TransConnect.Modules.Locations.Entities;

public sealed class SharedLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
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
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
}
