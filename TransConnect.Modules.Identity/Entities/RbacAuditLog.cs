namespace TransConnect.Modules.Identity.Entities;

public sealed class RbacAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorAccountId { get; set; }
    public string? RequestId { get; set; }
    public string SubjectType { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public string Action { get; set; } = "";
    public string ChangesJson { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
