namespace TransConnect.Modules.Operator.Dto.Responses;

public sealed record AuditResponse(Guid Id, Guid ActorAccountId, Guid SubjectId, string Action, string Details, DateTime CreatedAt);
