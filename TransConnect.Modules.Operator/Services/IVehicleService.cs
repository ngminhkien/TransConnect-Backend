using TransConnect.Modules.Operator.Dto.Requests;
using TransConnect.Modules.Operator.Dto.Responses;

namespace TransConnect.Modules.Operator.Services;

public interface IVehicleService
{
    Task<VehicleResponse> LinkAsync(Guid organizationId, LinkVehicleRequest request, CancellationToken ct);
    Task<IReadOnlyList<VehicleResponse>> ListAsync(Guid organizationId, CancellationToken ct);
    Task SuspendAsync(Guid organizationId, Guid linkId, SuspendVehicleRequest request, CancellationToken ct);
    Task EndAsync(Guid organizationId, Guid linkId, CancellationToken ct);
}
