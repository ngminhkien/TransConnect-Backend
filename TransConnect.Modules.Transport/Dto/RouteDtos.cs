namespace TransConnect.Modules.Transport.Dto;

public sealed class SaveRouteRequest
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Version { get; set; }
}

public sealed class StopRequest
{
    public Guid SharedLocationId { get; set; }
    public bool AllowPickup { get; set; }
    public bool AllowDropoff { get; set; }
    public int OffsetMinutes { get; set; }
    public int? DwellMinutes { get; set; }
}

public sealed class SaveDraftRequest
{
    public List<StopRequest> Stops { get; set; } = [];
    public string? Note { get; set; }
    public int Version { get; set; }
}

public sealed class VersionRequest
{
    public int Version { get; set; }
}

public sealed record StopResponse(Guid Id, int Sequence, Guid SharedLocationId, int LocationVersion, string Name,
    string AddressDetail, string ProvinceCode, string WardCode, decimal? Latitude, decimal? Longitude,
    bool AllowPickup, bool AllowDropoff, int OffsetMinutes, int? DwellMinutes);

public sealed record RevisionResponse(Guid Id, int RevisionNo, string Status, string? Note, DateTime CreatedAt,
    DateTime? PublishedAt, int Version, int StopCount, IReadOnlyList<StopResponse>? Stops = null);

public sealed record RouteResponse(Guid Id, Guid OrganizationId, Guid ServiceRegistrationId, string Code, string Name,
    string Status, string? StatusReason, Guid? CurrentRevisionId, int? CurrentRevisionNo, bool HasDraft, int Version,
    string? FirstStopName = null, string? LastStopName = null);

public sealed record RouteDetailResponse(RouteResponse Route, IReadOnlyList<RevisionResponse> Revisions,
    RevisionResponse? Current, RevisionResponse? Draft);
