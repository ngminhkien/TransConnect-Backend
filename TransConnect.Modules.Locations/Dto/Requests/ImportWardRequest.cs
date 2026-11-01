namespace TransConnect.Modules.Locations.Dto.Requests;

public sealed class ImportWardRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string ProvinceCode { get; set; } = "";
}
