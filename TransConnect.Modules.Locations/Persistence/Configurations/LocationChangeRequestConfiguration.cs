using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Locations.Entities;
namespace TransConnect.Modules.Locations.Persistence.Configurations;

public sealed class LocationChangeRequestConfiguration : IEntityTypeConfiguration<LocationChangeRequest>
{
    public void Configure(EntityTypeBuilder<LocationChangeRequest> builder)
    {
        builder.ToTable("ChangeRequests", table =>
        {
            table.HasCheckConstraint("CK_ChangeRequests_Kind", "[Kind] IN ('Create', 'Update', 'Archive')");
            table.HasCheckConstraint("CK_ChangeRequests_Status", "[Status] IN ('Pending', 'Approved', 'Rejected')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProvinceCode).HasMaxLength(20);
        builder.Property(x => x.WardCode).HasMaxLength(20);
        builder.Property(x => x.AddressDetail).HasMaxLength(500);
        builder.Property(x => x.AccessInstructions).HasMaxLength(1000);
        builder.Property(x => x.Latitude).HasPrecision(10, 7);
        builder.Property(x => x.Longitude).HasPrecision(10, 7);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.Name).HasMaxLength(255);
        builder.Property(x => x.Type).HasMaxLength(50);
        builder.Property(x => x.ConfirmationMethod).HasMaxLength(30);
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.Property(x => x.ReviewReason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.OrganizationId, x.Status });
    }
}
