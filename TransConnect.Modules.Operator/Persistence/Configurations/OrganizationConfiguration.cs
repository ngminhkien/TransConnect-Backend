using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Operator.Entities;

namespace TransConnect.Modules.Operator.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.RegistrationNumber).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.RegistrationNumber).IsUnique();
        builder.Property(x => x.ProposedOwnerEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.ReviewStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReviewNote).HasMaxLength(2000);
    }
}
