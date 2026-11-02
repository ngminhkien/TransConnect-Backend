using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Locations.Entities;
namespace TransConnect.Modules.Locations.Persistence.Configurations;

public sealed class CustomerAddressConfiguration : IEntityTypeConfiguration<CustomerAddress>
{
    public void Configure(EntityTypeBuilder<CustomerAddress> builder)
    {
        builder.ToTable("CustomerAddresses", table =>
        {
            table.HasCheckConstraint("CK_CustomerAddresses_Coordinates", "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)");
            table.HasCheckConstraint("CK_CustomerAddresses_Confirmation", "([ConfirmationMethod] IS NULL AND [ConfirmedAt] IS NULL AND [ConfirmedBy] IS NULL) OR ([ConfirmationMethod] IS NOT NULL AND [ConfirmationMethod] IN ('MapPin', 'ManualCoordinates') AND [ConfirmedAt] IS NOT NULL AND [ConfirmedBy] IS NOT NULL AND [Latitude] IS NOT NULL AND [Longitude] IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProvinceCode).HasMaxLength(20);
        builder.Property(x => x.WardCode).HasMaxLength(20);
        builder.Property(x => x.AddressDetail).HasMaxLength(500);
        builder.Property(x => x.AccessInstructions).HasMaxLength(1000);
        builder.Property(x => x.Latitude).HasPrecision(10, 7);
        builder.Property(x => x.Longitude).HasPrecision(10, 7);
        builder.HasOne<Ward>().WithMany().HasForeignKey(x => new { x.DatasetId, x.ProvinceCode, x.WardCode }).HasPrincipalKey(x => new { x.DatasetId, x.ProvinceCode, x.Code }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.Label).HasMaxLength(100);
        builder.Property(x => x.ConfirmationMethod).HasMaxLength(30);
        builder.HasIndex(x => new { x.CustomerId, x.IsActive });
    }
}
