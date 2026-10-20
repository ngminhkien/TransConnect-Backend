namespace TransConnect.Modules.Operator.Dto.Responses;

public sealed record EngagementProfileResponse(Guid EngagementId, Guid DriverId, string? FullName,
    string? ContactPhone, string? ContactAddress, bool IsHistorical, DateTime? CapturedAt, bool SnapshotAvailable);
