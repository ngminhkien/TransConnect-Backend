using TransConnect.Base.Entities;

namespace TransConnect.Modules.Operator.Entities;

public class OrganizationVehicle : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid VehicleId { get; set; }
    public bool IsSuspended { get; set; }
    public DateTime? EndedAt { get; set; }
    public Guid VerifiedBy { get; set; }
    public string VerificationReference { get; set; } = string.Empty;
}
