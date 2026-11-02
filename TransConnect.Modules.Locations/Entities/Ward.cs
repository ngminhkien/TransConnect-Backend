namespace TransConnect.Modules.Locations.Entities;

public sealed class Ward
{
    public Guid DatasetId { get; set; }
    public string Code { get; set; } = "";
    public string ProvinceCode { get; set; } = "";
    public string Name { get; set; } = "";
}
