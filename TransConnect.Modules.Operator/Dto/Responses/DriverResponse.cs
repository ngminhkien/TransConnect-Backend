namespace TransConnect.Modules.Operator.Dto.Responses;

public sealed record DriverResponse(Guid Id, Guid AccountId, string FullName, string ReviewStatus, string? ReviewNote,
    string? ContactPhone = null, string? ContactAddress = null, bool IsProfileComplete = false);
