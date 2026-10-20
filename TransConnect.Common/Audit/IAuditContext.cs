namespace TransConnect.Common.Audit;

public interface IAuditContext
{
    Guid? ActorAccountId { get; }
    string? RequestId { get; }
}
