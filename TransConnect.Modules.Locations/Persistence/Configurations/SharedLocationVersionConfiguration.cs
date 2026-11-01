using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Locations.Entities;
namespace TransConnect.Modules.Locations.Persistence.Configurations;

public sealed class SharedLocationVersionConfiguration : IEntityTypeConfiguration<SharedLocationVersion>
{
    public void Configure(EntityTypeBuilder<SharedLocationVersion> builder)
    {
        builder.ToTable("SharedLocationVersions", table =>
        {
            table.HasCheckConstraint("CK_SharedLocationVersions_Coordinates", "[Latitude] IS NOT NULL AND [Longitude] IS NOT NULL AND [Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180");
        });
        builder.HasKey(x => new { x.LocationId, x.Version });
        builder.HasOne<SharedLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.ProvinceCode).HasMaxLength(20);
        builder.Property(x => x.WardCode).HasMaxLength(20);
        builder.Property(x => x.AddressDetail).HasMaxLength(500);
        builder.Property(x => x.AccessInstructions).HasMaxLength(1000);
        builder.Property(x => x.Latitude).HasPrecision(10, 7);
        builder.Property(x => x.Longitude).HasPrecision(10, 7);
        builder.HasOne<Ward>().WithMany().HasForeignKey(x => new { x.DatasetId, x.ProvinceCode, x.WardCode }).HasPrincipalKey(x => new { x.DatasetId, x.ProvinceCode, x.Code }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Name).HasMaxLength(255);
        builder.Property(x => x.Type).HasMaxLength(50);
        builder.Property(x => x.ConfirmationMethod).HasMaxLength(30);
        builder.Property(x => x.Reason).HasMaxLength(1000);
    }
}
