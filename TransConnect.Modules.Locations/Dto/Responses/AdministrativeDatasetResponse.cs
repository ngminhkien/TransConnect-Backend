using TransConnect.Modules.Locations.Entities;

namespace TransConnect.Modules.Locations.Dto.Responses;

public sealed record AdministrativeDatasetResponse
{
    public Guid Id { get; init; }
    public string Source { get; init; } = "";
    public string Version { get; init; } = "";
    public string Checksum { get; init; } = "";
    public string ContentHash { get; init; } = "";
    public string? SourceCommit { get; init; }
    public string? License { get; init; }
    public string? Artifact { get; init; }
    public bool IsActive { get; init; }
    public DateTime ImportedAt { get; init; }

    public static AdministrativeDatasetResponse From(AdministrativeDataset value) => new()
    {
        Id = value.Id,
        Source = value.Source,
        Version = value.Version,
        Checksum = value.Checksum,
        ContentHash = value.ContentHash,
        SourceCommit = value.SourceCommit,
        License = value.License,
        Artifact = value.Artifact,
        IsActive = value.IsActive,
        ImportedAt = value.ImportedAt,
    };
}
