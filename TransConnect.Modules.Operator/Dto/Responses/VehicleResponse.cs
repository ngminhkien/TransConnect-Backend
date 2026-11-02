namespace TransConnect.Modules.Operator.Dto.Responses;

public sealed record VehicleResponse(Guid LinkId, Guid VehicleId, string RegistrationPlate, string Description,
    bool IsSuspended, DateTime? EndedAt, Guid? EndedBy = null, string? EndReason = null,
    string? VehicleKind = null, int? SeatCount = null, decimal? PermittedPayloadKg = null, bool IsProfileComplete = false);
