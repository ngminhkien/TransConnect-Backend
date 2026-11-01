using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Locations.Entities;
namespace TransConnect.Modules.Locations.Persistence.Configurations;

public sealed class AdministrativeDatasetConfiguration : IEntityTypeConfiguration<AdministrativeDataset>
{
    public void Configure(EntityTypeBuilder<AdministrativeDataset> builder)
    {
        builder.ToTable("Datasets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Source).HasMaxLength(500);
        builder.Property(x => x.Version).HasMaxLength(100);
        builder.Property(x => x.Checksum).HasMaxLength(64);
        builder.Property(x => x.ContentHash).HasMaxLength(64);
        builder.Property(x => x.SourceCommit).HasMaxLength(100);
        builder.Property(x => x.License).HasMaxLength(500);
        builder.Property(x => x.Artifact).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Source, x.Version }).IsUnique();
        builder.HasIndex(x => x.IsActive).IsUnique().HasFilter("[IsActive] = 1");
    }
}
