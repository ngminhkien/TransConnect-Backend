using Microsoft.EntityFrameworkCore;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Services;

namespace TransConnect.Modules.Identity.Persistence;

// Seed roles/permissions/admin mặc định để có thể test ngay sau khi tạo DB, tránh
// tình trạng "trứng-gà" (cần Admin để gán quyền, nhưng chưa có ai có quyền Admin).
public static class IdentitySeeder
{
    public const string AdminEmail = "admin@hotmail.com";
    public const string AdminDefaultPassword = "123456"; // Đổi ngay sau lần chạy đầu tiên.

    private static readonly string[] DefaultPermissions =
    [
        "roles.manage",
        "permissions.manage",
        "users.manage"
    ];

    public static async Task SeedAsync(
        IdentityDbContext context,
        IPasswordHasher passwordHasher,
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

        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin", cancellationToken);
        var adminRoleCreated = adminRole is null;
        if (adminRole is null)
        {
            adminRole = new Role { Name = "Admin", Description = "Toàn quyền quản trị hệ thống" };
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

        var admin = await context.Users.FirstOrDefaultAsync(u => u.Email == AdminEmail, cancellationToken);
        var adminCreated = admin is null;
        if (admin is null)
        {
            admin = new User
            {
                Email = AdminEmail,
                FullName = "System Admin",
                PasswordHash = passwordHasher.Hash(AdminDefaultPassword),
                IsActive = true,
                // Tài khoản seed sẵn, không đi qua luồng đăng ký nên đánh dấu đã xác thực luôn.
                EmailVerifiedAt = DateTime.UtcNow
            };
            context.Users.Add(admin);
            await context.SaveChangesAsync(cancellationToken);
        }

        // Role được tạo lại có ID mới; liên kết tới role đã xóa không cấp quyền nữa.
        // Chỉ khôi phục tài khoản seed khi tạo user/role mặc định, không gán lại cho user khác.
        if ((adminCreated || adminRoleCreated) && !await context.UserRoles.AnyAsync(
                ur => ur.UserId == admin.Id && ur.RoleId == adminRole.Id, cancellationToken))
        {
            context.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
