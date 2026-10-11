using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Dto.Responses;

namespace TransConnect.Modules.Identity.Services;

public interface IRoleService
{
    Task<IReadOnlyList<RoleResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<RoleResponse> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);
    Task<RoleResponse> UpdateAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid roleId, CancellationToken cancellationToken = default);

    Task AssignPermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default);
    Task RemovePermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default);

    Task AssignToUserAsync(Guid roleId, Guid userId, CancellationToken cancellationToken = default);
    Task RemoveFromUserAsync(Guid roleId, Guid userId, CancellationToken cancellationToken = default);
}
