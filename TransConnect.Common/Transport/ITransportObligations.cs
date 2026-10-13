namespace TransConnect.Common.Transport;

// Implemented by Operations/Booking when stage 2 introduces assignments and orders.
public interface ITransportObligations
{
    Task<bool> HasDriverObligationsAsync(Guid organizationId, Guid driverId, CancellationToken ct);
    Task<bool> HasVehicleObligationsAsync(Guid organizationId, Guid vehicleId, CancellationToken ct);
}
