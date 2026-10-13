using TransConnect.Common.Transport;

namespace TransConnect.Modules.Operator.Services;

// Stage 1 contains no trip/order writers. Replace this registration BEFORE enabling stage 2.
public sealed class FoundationTransportObligations : ITransportObligations
{
    public Task<bool> HasDriverObligationsAsync(Guid organizationId, Guid driverId, CancellationToken ct) => Task.FromResult(false);
    public Task<bool> HasVehicleObligationsAsync(Guid organizationId, Guid vehicleId, CancellationToken ct) => Task.FromResult(false);
}
