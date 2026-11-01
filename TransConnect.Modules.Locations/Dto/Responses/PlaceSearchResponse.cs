namespace TransConnect.Modules.Locations.Dto.Responses;

// A provider candidate is not an approved SharedLocation and has no VieGo locationId/version.
public sealed record PlaceSearchResponse(string SourceId, string Name, string DisplayName,
    decimal Latitude, decimal Longitude, string Source = "OpenStreetMap");
