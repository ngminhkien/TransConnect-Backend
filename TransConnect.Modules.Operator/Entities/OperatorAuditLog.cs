namespace TransConnect.Modules.Operator.Entities;

// Append-only through the service; no public update/delete endpoint.
public class OperatorAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid ActorAccountId { get; set; }
    public Guid SubjectId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
