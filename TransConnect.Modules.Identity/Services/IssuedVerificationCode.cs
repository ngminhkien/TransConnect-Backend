namespace TransConnect.Modules.Identity.Services;

public sealed record IssuedVerificationCode(string Code, Guid Id, DateTime ExpiresAt);
