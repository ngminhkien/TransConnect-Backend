namespace TransConnect.Modules.Locations.Dto.Requests;

public sealed class ReviewLocationRequest
{
    public bool Approve { get; set; }
    public int Version { get; set; }
    public string Reason { get; set; } = "";
}
