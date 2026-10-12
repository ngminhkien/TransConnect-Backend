using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Dto.Responses;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Services;

public class RoleService(IdentityDbContext dbContext) : IRoleService
{
    public async Task<IReadOnlyList<RoleResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        // Include trước rồi map ở phía client — ToResponse không dịch được sang SQL.
        var roles = await dbContext.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(ToResponse).ToList();
    }

    public async Task<RoleResponse> GetByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await FindRoleWithPermissionsAsync(roleId, cancellationToken)
            ?? throw new NotFoundException("Vai trò", roleId);

        return ToResponse(role);
    }

    public async Task<RoleResponse> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        if (await dbContext.Roles.AnyAsync(r => r.Name == request.Name, cancellationToken))
        {
            throw new ConflictException($"Vai trò '{request.Name}' đã tồn tại.");
        }

        var role = new Role { Name = request.Name, Description = request.Description };
        dbContext.Roles.Add(role);
        await SaveUniqueRoleAsync(role, cancellationToken);

        return ToResponse(role);
    }

    public async Task<RoleResponse> UpdateAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await dbContext.Roles.FindAsync([roleId], cancellationToken)
            ?? throw new NotFoundException("Vai trò", roleId);

        var nameTaken = await dbContext.Roles.AnyAsync(r => r.Id != roleId && r.Name == request.Name, cancellationToken);
        if (nameTaken)
        {
            throw new ConflictException($"Vai trò '{request.Name}' đã tồn tại.");
        }

        role.Name = request.Name;
        role.Description = request.Description;
        await SaveUniqueRoleAsync(role, cancellationToken);

        return ToResponse(role);
    }

    public async Task DeleteAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        var role = await dbContext.Roles.FindAsync([roleId], cancellationToken)
            ?? throw new NotFoundException("Vai trò", roleId);

        // Soft-delete (đúng thiết kế BaseEntity.DeletedAt) thay vì xoá cứng — role
        // biến mất khỏi mọi query nhờ soft-delete filter của AppDbContextBase.
        role.DeletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignPermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        await EnsureRoleExistsAsync(roleId, cancellationToken);
        await EnsurePermissionExistsAsync(permissionId, cancellationToken);

        var exists = await dbContext.RolePermissions
            .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, cancellationToken);

        if (!exists)
        {
            dbContext.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemovePermissionAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken = default)
    {
        var link = await dbContext.RolePermissions
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, cancellationToken);

        if (link is not null)
        {
            dbContext.RolePermissions.Remove(link);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AssignToUserAsync(Guid roleId, Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureRoleExistsAsync(roleId, cancellationToken);

        var userExists = await dbContext.Users.AnyAsync(u => u.Id == userId, cancellationToken);
        if (!userExists)
        {
            throw new NotFoundException("Người dùng", userId);
        }

        var exists = await dbContext.UserRoles
            .AnyAsync(ur => ur.RoleId == roleId && ur.UserId == userId, cancellationToken);

        if (!exists)
        {
            dbContext.UserRoles.Add(new UserRole { RoleId = roleId, UserId = userId });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task RemoveFromUserAsync(Guid roleId, Guid userId, CancellationToken cancellationToken = default)
    {
        var link = await dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.RoleId == roleId && ur.UserId == userId, cancellationToken);

        if (link is not null)
        {
            dbContext.UserRoles.Remove(link);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<Role?> FindRoleWithPermissionsAsync(Guid roleId, CancellationToken cancellationToken) =>
        await dbContext.Roles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken);

    private async Task EnsureRoleExistsAsync(Guid roleId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Roles.AnyAsync(r => r.Id == roleId, cancellationToken))
        {
            throw new NotFoundException("Vai trò", roleId);
        }
    }

    private async Task EnsurePermissionExistsAsync(Guid permissionId, CancellationToken cancellationToken)
    {
        if (!await dbContext.Permissions.AnyAsync(p => p.Id == permissionId, cancellationToken))
        {
            throw new NotFoundException("Quyền", permissionId);
        }
    }

    private Task SaveUniqueRoleAsync(Role role, CancellationToken cancellationToken) =>
        IdentityUniqueWrite.SaveAsync(dbContext,
            ct => dbContext.Roles.AnyAsync(r => r.Id != role.Id && r.Name == role.Name, ct),
            $"Vai trò '{role.Name}' đã tồn tại.", cancellationToken);

    private static RoleResponse ToResponse(Role role) => new()
    {
        Id = role.Id,
        Name = role.Name,
        Description = role.Description,
        Permissions = role.RolePermissions.Select(rp => rp.Permission.Code).ToList()
    };
}
