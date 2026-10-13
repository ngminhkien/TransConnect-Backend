using TransConnect.Base.Entities;

namespace TransConnect.Modules.Operator.Entities;

// DOM-DRIVER-001: profile is distinct from the Identity account.
public class Driver : BaseEntity
{
    public Guid AccountId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = "Pending";
    public string? ReviewNote { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
