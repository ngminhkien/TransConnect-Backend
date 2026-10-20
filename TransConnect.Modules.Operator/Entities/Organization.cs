using TransConnect.Base.Entities;

namespace TransConnect.Modules.Operator.Entities;

// DOM-ORG-001: registration is independent from a login account.
public class Organization : BaseEntity
{
    public Guid? CreatedByUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string ProposedOwnerEmail { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = "Pending";
    public string? ReviewNote { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int EmployeeLimit { get; set; }
    public int? DriverLimit { get; set; }
    public string? LegalEntityType { get; set; }
    public string? RegisteredAddress { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
}
