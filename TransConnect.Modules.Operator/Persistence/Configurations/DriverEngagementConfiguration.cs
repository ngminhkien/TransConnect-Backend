using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Operator.Entities;

namespace TransConnect.Modules.Operator.Persistence.Configurations;

public sealed class DriverEngagementConfiguration : IEntityTypeConfiguration<DriverEngagement>
{
    public void Configure(EntityTypeBuilder<DriverEngagement> builder)
    {
        builder.ToTable("Engagements");
        builder.Property(x => x.DriverNameSnapshot).HasMaxLength(200);
        builder.Property(x => x.DriverPhoneSnapshot).HasMaxLength(20);
        builder.Property(x => x.DriverAddressSnapshot).HasMaxLength(500);
        builder.HasOne<OrganizationInvitation>().WithMany().HasForeignKey(x => x.InvitationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.OrganizationId, x.DriverId }).IsUnique().HasFilter("[EndedAt] IS NULL AND [RevokedAt] IS NULL");
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Driver>().WithMany().HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
    }
}
