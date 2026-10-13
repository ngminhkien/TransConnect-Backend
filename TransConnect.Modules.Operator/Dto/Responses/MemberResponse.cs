namespace TransConnect.Modules.Operator.Dto.Responses;

public sealed record MemberResponse(Guid Id, Guid AccountId, string Role, bool IsActive);
