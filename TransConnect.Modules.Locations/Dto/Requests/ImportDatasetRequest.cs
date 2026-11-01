namespace TransConnect.Modules.Locations.Dto.Requests;

public sealed class ImportDatasetRequest
{
    public string Source { get; set; } = "";
    public string Version { get; set; } = "";
    public string Checksum { get; set; } = "";
    public string? SourceCommit { get; set; }
    public string? License { get; set; }
    public string? Artifact { get; set; }
    public List<ImportProvinceRequest> Provinces { get; set; } = [];
    public List<ImportWardRequest> Wards { get; set; } = [];
}
