namespace TransConnect.Modules.Operator.Dto.Responses;

public sealed record EngagementResponse(Guid Id, Guid OrganizationId, Guid DriverId, DateTime ExpiresAt, DateTime? AcceptedAt, DateTime? EndedAt, DateTime? RevokedAt);
