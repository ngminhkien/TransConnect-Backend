namespace TransConnect.Modules.Operator.Entities;

public sealed class ProfileReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SubjectType { get; set; } = "";
    public Guid SubjectId { get; set; }
    public string Status { get; set; } = "";
    public string? Note { get; set; }
    public Guid ActorAccountId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
