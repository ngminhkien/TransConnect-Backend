using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Compliance.Entities;

namespace TransConnect.Modules.Compliance.Persistence.Configurations;

public sealed class DemoProfileDocumentConfiguration : IEntityTypeConfiguration<DemoProfileDocument>
{
    public void Configure(EntityTypeBuilder<DemoProfileDocument> builder)
    {
        builder.ToTable("DemoProfileDocuments", t => t.HasCheckConstraint("CK_DemoProfileDocuments_Type",
            "([SubjectType] = 'Driver' AND [DocumentType] = 'DriverLicense' AND [ScopeOrganizationId] IS NULL) OR " +
            "([SubjectType] = 'Vehicle' AND [DocumentType] = 'VehicleRegistration' AND [ScopeOrganizationId] IS NOT NULL)"));
        builder.Property(x => x.SubjectType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DocumentType).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => new { x.SubjectType, x.SubjectId, x.DocumentType }).IsUnique().HasFilter("[ScopeOrganizationId] IS NULL");
        builder.HasIndex(x => new { x.SubjectType, x.SubjectId, x.DocumentType, x.ScopeOrganizationId }).IsUnique()
            .HasFilter("[ScopeOrganizationId] IS NOT NULL");
    }
}
