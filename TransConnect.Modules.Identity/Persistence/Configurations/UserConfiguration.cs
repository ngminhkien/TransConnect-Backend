using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Identity.Entities;

namespace TransConnect.Modules.Identity.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", t => t.HasCheckConstraint("CK_Users_AccountGroup", "[AccountGroup] IN ('Customer', 'Operator', 'InternalAdmin')"));

        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.AccountGroup).IsRequired().HasMaxLength(20);
        builder.HasIndex(u => new { u.AccountGroup, u.Email }).IsUnique();

        builder.Property(u => u.PhoneNumber).HasMaxLength(20);
        builder.Property(u => u.PasswordHash);
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.AvatarUrl).HasMaxLength(500);

        builder.Property(u => u.GoogleId).HasMaxLength(255);
        // Filtered index: cho phép nhiều user có GoogleId = NULL (chưa liên kết Google).
        builder.HasIndex(u => new { u.AccountGroup, u.GoogleId }).IsUnique().HasFilter("[GoogleId] IS NOT NULL");
    }
}
