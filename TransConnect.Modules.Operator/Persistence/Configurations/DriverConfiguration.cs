using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Operator.Entities;

namespace TransConnect.Modules.Operator.Persistence.Configurations;

public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("Drivers", t => t.HasCheckConstraint("CK_Drivers_ReviewStatus", "[ReviewStatus] IN ('Pending', 'Approved', 'Rejected')"));
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ContactPhone).HasMaxLength(20);
        builder.Property(x => x.ContactAddress).HasMaxLength(500);
        builder.Property(x => x.ReviewStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ReviewNote).HasMaxLength(2000);
        builder.HasIndex(x => x.AccountId).IsUnique();
    }
}
