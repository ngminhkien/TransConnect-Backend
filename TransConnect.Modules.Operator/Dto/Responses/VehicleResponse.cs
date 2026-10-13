namespace TransConnect.Modules.Operator.Dto.Responses;

public sealed record VehicleResponse(Guid LinkId, Guid VehicleId, string RegistrationPlate, string Description, bool IsSuspended, DateTime? EndedAt);
