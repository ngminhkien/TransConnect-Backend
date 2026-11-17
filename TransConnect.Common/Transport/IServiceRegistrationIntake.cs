namespace TransConnect.Common.Transport;

/// <summary>Called by Operator after an organization is approved: creates Pending service registrations
/// for the declared RequestedServiceTypes. Idempotent; never approves anything.</summary>
public interface IServiceRegistrationIntake
{
    Task CreatePendingAsync(Guid organizationId, IReadOnlyCollection<string> serviceTypeCodes, Guid actorAccountId, CancellationToken ct = default);
}

public sealed class NoServiceRegistrationIntake : IServiceRegistrationIntake
{
    public Task CreatePendingAsync(Guid organizationId, IReadOnlyCollection<string> serviceTypeCodes, Guid actorAccountId, CancellationToken ct = default)
        => Task.CompletedTask;
}
