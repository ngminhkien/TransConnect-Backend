using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Compliance.Entities;

namespace TransConnect.Modules.Compliance.Persistence.Configurations;

public sealed class DemoDocumentConfiguration : IEntityTypeConfiguration<DemoDocument>
{
    public void Configure(EntityTypeBuilder<DemoDocument> builder)
    {
        builder.ToTable("DemoDocuments");
        builder.HasOne<DemoProfileDocument>().WithMany().HasForeignKey(x => x.ProfileDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ProfileDocumentId, x.VersionNumber }).IsUnique().HasFilter("[ProfileDocumentId] IS NOT NULL");
        builder.Property(x => x.Bucket).HasMaxLength(63).IsRequired();
        builder.Property(x => x.ObjectKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.Scenario).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ScanResult).HasMaxLength(40).IsRequired();
        builder.Property(x => x.OcrText).HasMaxLength(2000);
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.HasIndex(x => new { x.AccountId, x.CreatedAt });
        builder.HasIndex(x => new { x.Bucket, x.ObjectKey }).IsUnique();
    }
}
