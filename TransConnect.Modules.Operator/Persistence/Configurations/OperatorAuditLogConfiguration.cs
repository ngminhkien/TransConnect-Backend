using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Operator.Entities;

namespace TransConnect.Modules.Operator.Persistence.Configurations;

public sealed class OperatorAuditLogConfiguration : IEntityTypeConfiguration<OperatorAuditLog>
{
    public void Configure(EntityTypeBuilder<OperatorAuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Details).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
    }
}
