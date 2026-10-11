using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Entities;

namespace TransConnect.Data;

// DbContext base cho từng module kế thừa: tự áp soft-delete query filter cho mọi
// entity kế thừa BaseEntity, và tự set UpdatedAt khi entity bị Modified.
public abstract class AppDbContextBase(DbContextOptions options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var deletedAtProperty = Expression.Property(parameter, nameof(BaseEntity.DeletedAt));
            var isNull = Expression.Equal(deletedAtProperty, Expression.Constant(null, typeof(DateTime?)));
            var lambda = Expression.Lambda(isNull, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }

    // Override 2 overload nhận acceptAllChangesOnSuccess (chứ không phải overload ngắn):
    // các overload ngắn SaveChanges()/SaveChangesAsync(ct) của EF Core đều gọi xuống 2 overload
    // này, nên đặt logic ở đây mới bịt được mọi đường ghi.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditInfo();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditInfo()
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.UtcNow;
            }
        }
    }
}
