using TransConnect.Base.Entities;

namespace TransConnect.Modules.Operator.Entities;

public class OrganizationMember : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid AccountId { get; set; }
    public string Role { get; set; } = "Dispatcher";
    public bool IsActive { get; set; } = true;
}
