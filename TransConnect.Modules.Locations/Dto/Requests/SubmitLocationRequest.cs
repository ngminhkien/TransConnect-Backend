namespace TransConnect.Modules.Locations.Dto.Requests;

public sealed class SubmitLocationRequest
{
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
    public Guid OrganizationId { get; set; }
    public string Kind { get; set; } = "";
    public Guid? LocationId { get; set; }
    public int? BaseVersion { get; set; }
    public string Reason { get; set; } = "";
}
