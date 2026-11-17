using Microsoft.EntityFrameworkCore;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Services;

namespace TransConnect.Modules.Identity.Persistence;

// Seed roles/permissions mặc định. KHÔNG tạo tài khoản: Admin đầu tiên được tạo bằng lệnh
// `create-admin` (AdminBootstrapper) để mật khẩu không bao giờ nằm trong source/cấu hình.
public static class IdentitySeeder
{
    public const string AdminRoleName = "Admin";

    private static readonly string[] DefaultPermissions =
    [
        "roles.manage",
        "permissions.manage",
        "users.manage",
        "admins.manage",
        "organizations.manage",
        "locations.manage",
        "services.review"
    ];

    public static async Task SeedAsync(
        IdentityDbContext context,
        CancellationToken cancellationToken = default)
    {
        foreach (var code in DefaultPermissions)
        {
            if (!await context.Permissions.AnyAsync(p => p.Code == code, cancellationToken))
            {
                context.Permissions.Add(new Permission { Code = code });
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == AdminRoleName, cancellationToken);
        if (adminRole is null)
        {
            adminRole = new Role { Name = AdminRoleName, Description = "Toàn quyền quản trị hệ thống" };
            context.Roles.Add(adminRole);
            await context.SaveChangesAsync(cancellationToken);
        }

        if (!await context.Roles.AnyAsync(r => r.Name == "User", cancellationToken))
        {
            context.Roles.Add(new Role { Name = "User", Description = "Người dùng mặc định sau khi đăng ký" });
            await context.SaveChangesAsync(cancellationToken);
        }

        var allPermissionIds = await context.Permissions.Select(p => p.Id).ToListAsync(cancellationToken);
        var adminPermissionIds = await context.RolePermissions
            .Where(rp => rp.RoleId == adminRole.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(cancellationToken);

        foreach (var permissionId in allPermissionIds.Except(adminPermissionIds))
        {
            context.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionId = permissionId });
        }

        await context.SaveChangesAsync(cancellationToken);
        // Role Admin được tạo lại có ID mới nên không ai còn giữ nó; khôi phục quyền cho một
        // tài khoản cụ thể bằng `create-admin --email <email>` thay vì tự gán ở đây.
    }
}
