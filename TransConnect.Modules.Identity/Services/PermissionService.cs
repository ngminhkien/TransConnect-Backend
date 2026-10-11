using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Dto.Responses;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Services;

public class PermissionService(IdentityDbContext dbContext) : IPermissionService
{
    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PermissionResponse>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Permissions
            .OrderBy(p => p.Code)
            .Select(p => new PermissionResponse { Id = p.Id, Code = p.Code, Description = p.Description })
            .ToListAsync(cancellationToken);

    public async Task<PermissionResponse> CreateAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await dbContext.Permissions.AnyAsync(p => p.Code == request.Code, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"Quyền '{request.Code}' đã tồn tại.");
        }

        var permission = new Permission { Code = request.Code, Description = request.Description };
        dbContext.Permissions.Add(permission);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PermissionResponse { Id = permission.Id, Code = permission.Code, Description = permission.Description };
    }

    public async Task DeleteAsync(Guid permissionId, CancellationToken cancellationToken = default)
    {
        var permission = await dbContext.Permissions.FindAsync([permissionId], cancellationToken)
            ?? throw new NotFoundException("Quyền", permissionId);

        // Soft-delete (đúng thiết kế BaseEntity.DeletedAt) thay vì xoá cứng.
        permission.DeletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
