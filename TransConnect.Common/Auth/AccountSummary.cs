namespace TransConnect.Common.Auth;

public sealed record AccountSummary(Guid AccountId, string Email, string Group, bool EmailVerified);
