namespace TransConnect.Modules.Locations.Services;

public sealed class PlaceSearchOptions
{
    public const string Section = "Locations:PlaceSearch";
    public bool Enabled { get; set; } = true;
    public string Endpoint { get; set; } = "https://nominatim.openstreetmap.org/search";
    public string UserAgent { get; set; } = "VieGo/0.1 (transport-public-place-search)";
}
