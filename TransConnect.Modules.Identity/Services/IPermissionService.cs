using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Dto.Responses;

namespace TransConnect.Modules.Identity.Services;

public interface IPermissionService
{
    Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PermissionResponse> CreateAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid permissionId, CancellationToken cancellationToken = default);
}
