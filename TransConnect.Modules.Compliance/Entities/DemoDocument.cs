using TransConnect.Base.Entities;

namespace TransConnect.Modules.Compliance.Entities;

// DOM-COMPLIANCE-DEMO-001: synthetic data only, never a verified legal document.
public sealed class DemoDocument : BaseEntity
{
    public Guid AccountId { get; set; }
    public Guid? ProfileDocumentId { get; set; }
    public int? VersionNumber { get; set; }
    public string Bucket { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    public string Status { get; set; } = "Uploading";
    public string Scenario { get; set; } = "";
    public string ScanResult { get; set; } = "NOT_SCANNED";
    public string? OcrText { get; set; }
    public DateTime? SimulatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    // Optimistic concurrency: simulation, review and delete cannot overwrite one another.
    public Guid Revision { get; set; } = Guid.NewGuid();
}
