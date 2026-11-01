namespace TransConnect.Common.Transport;

/// <summary>Checks live organization ownership without a Locations dependency on Operator storage.</summary>
public interface ILocationOrganizationAccess
{
    Task RequireOwnerAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken = default);
}
