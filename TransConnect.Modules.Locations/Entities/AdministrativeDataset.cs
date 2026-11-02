namespace TransConnect.Modules.Locations.Entities;

public sealed class AdministrativeDataset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Source { get; set; } = "";
    public string Version { get; set; } = "";
    public string Checksum { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string? SourceCommit { get; set; }
    public string? License { get; set; }
    public string? Artifact { get; set; }
    public bool IsActive { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}
