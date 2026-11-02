using TransConnect.Base.Entities;

namespace TransConnect.Modules.Operator.Entities;

public class DriverEngagement : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid DriverId { get; set; }
    public Guid? InvitationId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? DriverNameSnapshot { get; set; }
    public string? DriverPhoneSnapshot { get; set; }
    public string? DriverAddressSnapshot { get; set; }
}
