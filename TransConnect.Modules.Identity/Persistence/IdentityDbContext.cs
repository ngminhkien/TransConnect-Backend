using Microsoft.EntityFrameworkCore;
using TransConnect.Data;
using TransConnect.Modules.Identity.Entities;

namespace TransConnect.Modules.Identity.Persistence;

public class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : AppDbContextBase(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Schema riêng cho module Identity trong cùng 1 SQL Server — giữ ranh giới
        // logic giữa các module dù dùng chung DB (xem docs/base-libraries-guidelines.md).
        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);

        // Gọi sau cùng: AppDbContextBase áp soft-delete query filter cho các entity
        // kế thừa BaseEntity, cần chạy sau khi model đã được cấu hình đầy đủ.
        base.OnModelCreating(modelBuilder);
    }
}
