namespace TransConnect.Modules.Locations.Dto.Requests;

public sealed class SaveAddressRequest
{
    public Guid DatasetId { get; set; }
    public string ProvinceCode { get; set; } = "";
    public string WardCode { get; set; } = "";
    public string AddressDetail { get; set; } = "";
    public string? AccessInstructions { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Label { get; set; }
    public string? ConfirmationMethod { get; set; }
    public int Version { get; set; }
}
