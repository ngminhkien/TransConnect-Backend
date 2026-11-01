namespace TransConnect.Modules.Locations.Entities;

public sealed class CustomerAddress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid DatasetId { get; set; }
    public string ProvinceCode { get; set; } = "";
    public string WardCode { get; set; } = "";
    public string AddressDetail { get; set; } = "";
    public string? AccessInstructions { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Label { get; set; }
    public string? ConfirmationMethod { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = 1;
}
