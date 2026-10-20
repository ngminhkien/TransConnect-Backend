using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Operator.Entities;

namespace TransConnect.Modules.Operator.Persistence.Configurations;

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("Members", t => t.HasCheckConstraint("CK_Members_Role", "[Role] IN ('OrganizationOwner', 'Manager', 'Dispatcher')"));
        builder.HasIndex(x => x.OrganizationId).IsUnique()
            .HasFilter("[Role] = 'OrganizationOwner' AND [IsActive] = 1 AND [DeletedAt] IS NULL");
        builder.Property(x => x.Role).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.AccountId).IsUnique();
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}
