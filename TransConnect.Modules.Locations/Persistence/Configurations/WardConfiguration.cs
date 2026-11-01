using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Locations.Entities;
namespace TransConnect.Modules.Locations.Persistence.Configurations;

public sealed class WardConfiguration : IEntityTypeConfiguration<Ward>
{
    public void Configure(EntityTypeBuilder<Ward> builder)
    {
        builder.ToTable("Wards");
        builder.HasKey(x => new { x.DatasetId, x.Code });
        builder.HasAlternateKey(x => new { x.DatasetId, x.ProvinceCode, x.Code });
        builder.HasOne<Province>().WithMany().HasForeignKey(x => new { x.DatasetId, x.ProvinceCode }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Code).HasMaxLength(20);
        builder.Property(x => x.Name).HasMaxLength(255);
        builder.Property(x => x.ProvinceCode).HasMaxLength(20);
    }
}
