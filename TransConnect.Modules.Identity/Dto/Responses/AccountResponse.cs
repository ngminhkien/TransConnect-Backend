namespace TransConnect.Modules.Identity.Dto.Responses;

public sealed record AccountResponse(Guid Id, string AccountGroup, string Email, string FullName,
    string? PhoneNumber, string? AvatarUrl, bool IsActive, bool EmailVerified);

public sealed record AccountAuditResponse(Guid Id, bool? WasActive, bool? IsActive, string? Reason, DateTime CreatedAt, string? RequestId);
