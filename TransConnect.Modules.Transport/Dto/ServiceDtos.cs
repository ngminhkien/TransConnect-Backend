namespace TransConnect.Modules.Transport.Dto;

public sealed record ServiceTypeResponse(string Code, string Name, string Description, IReadOnlyList<string> AllowedModes, bool IsOperational);

public sealed record RegistrationEventResponse(Guid Id, string Action, string Status, string? Note, string? ScopeNote,
    Guid ActorAccountId, DateTime CreatedAt);

public sealed record RegistrationResponse(Guid Id, Guid OrganizationId, string? OrganizationName, string? RegistrationNumber,
    string ServiceTypeCode, string ServiceTypeName, bool IsOperational, string Status, string? ScopeNote, string? ReviewNote,
    DateTime? ReviewedAt, DateTime SubmittedAt, int Version, IReadOnlyList<RegistrationEventResponse>? Events = null);

public sealed class SubmitRegistrationRequest
{
    public string ServiceTypeCode { get; set; } = "";
    public string? ScopeNote { get; set; }
}

public sealed class ResubmitRegistrationRequest
{
    public string? ScopeNote { get; set; }
    public int Version { get; set; }
}

public sealed class NoteRequest
{
    public string? Note { get; set; }
    public int Version { get; set; }
}

public sealed class ReviewRegistrationRequest
{
    // "Approved" hoặc "Rejected".
    public string Decision { get; set; } = "";
    public string? Note { get; set; }
    public int Version { get; set; }
}
