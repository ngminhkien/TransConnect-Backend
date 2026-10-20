using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Identity.Entities;

namespace TransConnect.Modules.Identity.Persistence.Configurations;

public sealed class RbacAuditLogConfiguration : IEntityTypeConfiguration<RbacAuditLog>
{
    public void Configure(EntityTypeBuilder<RbacAuditLog> builder)
    {
        builder.ToTable("RbacAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequestId).HasMaxLength(200);
        builder.Property(x => x.SubjectType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SubjectId).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(20).IsRequired();
        builder.Property(x => x.ChangesJson).IsRequired();
        builder.HasIndex(x => x.CreatedAt);
    }
}
