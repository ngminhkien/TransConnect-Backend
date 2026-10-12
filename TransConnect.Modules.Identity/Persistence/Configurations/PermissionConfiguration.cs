using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Identity.Entities;

namespace TransConnect.Modules.Identity.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.Property(p => p.Code).IsRequired().HasMaxLength(150);
        builder.HasIndex(p => p.Code).IsUnique().HasFilter("[DeletedAt] IS NULL");

        builder.Property(p => p.Description).HasMaxLength(500);
    }
}
