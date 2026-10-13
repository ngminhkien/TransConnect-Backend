using TransConnect.Base.Entities;

namespace TransConnect.Modules.Operator.Entities;

public class OrganizationInvitation : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Dispatcher";
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? AcceptedBy { get; set; }
}
