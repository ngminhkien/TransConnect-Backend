namespace TransConnect.Common.Transport;

public sealed record TransportOrganization(Guid Id, string Name, string RegistrationNumber, string ReviewStatus,
    string? LegalEntityType, IReadOnlyList<string> RequestedServiceTypes);

public sealed record TransportMembership(Guid OrganizationId, Guid AccountId, string Role)
{
    public bool IsOwner => Role == "OrganizationOwner";
}

public sealed record TransportVehicle(Guid Id, Guid OrganizationId, string RegistrationPlate, string? VehicleKind, int? SeatCount);

/// <summary>Operator-owned reads for the Transport module; Transport never touches Operator storage.</summary>
public interface ITransportOrganizationAccess
{
    /// <summary>Active member of an approved organization with a verified Operator account; otherwise Forbidden.</summary>
    Task<TransportMembership> RequireMemberAsync(Guid organizationId, Guid accountId, CancellationToken ct = default);
    Task<TransportOrganization?> FindAsync(Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, TransportOrganization>> FindManyAsync(IReadOnlyCollection<Guid> organizationIds, CancellationToken ct = default);
    /// <summary>Vehicle currently managed (not ended) by the organization, or null.</summary>
    Task<TransportVehicle?> FindManagedVehicleAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default);
}
