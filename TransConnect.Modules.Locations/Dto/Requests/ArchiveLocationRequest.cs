namespace TransConnect.Modules.Locations.Dto.Requests;

public sealed class ArchiveLocationRequest
{
    public int Version { get; set; }
    public string Reason { get; set; } = "";
}
