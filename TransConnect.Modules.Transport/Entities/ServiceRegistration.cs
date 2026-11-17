using TransConnect.Base.Entities;

namespace TransConnect.Modules.Transport.Entities;

// Lớp 2: tổ chức đăng ký một loại dịch vụ, Admin duyệt thủ công (Q-M2-SERVICE-003).
public sealed class ServiceRegistration : BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string ServiceTypeCode { get; set; } = "";
    public string Status { get; set; } = RegistrationStatuses.Pending;
    public string? ScopeNote { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid SubmittedBy { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public int Version { get; set; } = 1;
}

public static class RegistrationStatuses
{
    public const string Pending = "Pending", Approved = "Approved", Rejected = "Rejected", Suspended = "Suspended", Withdrawn = "Withdrawn";
}

// Lịch sử bất biến của đăng ký dịch vụ.
public sealed class ServiceRegistrationEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RegistrationId { get; set; }
    public string Action { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Note { get; set; }
    public string? ScopeNote { get; set; }
    public Guid ActorAccountId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
