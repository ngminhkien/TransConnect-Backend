using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Locations.Entities;
namespace TransConnect.Modules.Locations.Persistence.Configurations;

public sealed class SharedLocationConfiguration : IEntityTypeConfiguration<SharedLocation>
{
    public void Configure(EntityTypeBuilder<SharedLocation> builder)
    {
        builder.ToTable("SharedLocations", table =>
        {
            table.HasCheckConstraint("CK_SharedLocations_Coordinates", "[Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180");
            table.HasCheckConstraint("CK_SharedLocations_Confirmation", "[ConfirmationMethod] IS NOT NULL AND [ConfirmationMethod] IN ('MapPin', 'ManualCoordinates') AND [ConfirmedAt] IS NOT NULL AND [ConfirmedBy] IS NOT NULL");
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
        builder.Property(x => x.Name).HasMaxLength(255);
        builder.Property(x => x.Type).HasMaxLength(50);
        builder.Property(x => x.ConfirmationMethod).HasMaxLength(30);
        builder.HasIndex(x => new { x.IsActive, x.ProvinceCode, x.Type });
    }
}
