using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Operator.Entities;

namespace TransConnect.Modules.Operator.Persistence.Configurations;

public sealed class OrganizationVehicleConfiguration : IEntityTypeConfiguration<OrganizationVehicle>
{
    public void Configure(EntityTypeBuilder<OrganizationVehicle> builder)
    {
        builder.ToTable("OrganizationVehicles");
        builder.Property(x => x.EndReason).HasMaxLength(1000);
        builder.Property(x => x.VerificationReference).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => x.VehicleId).IsUnique().HasFilter("[EndedAt] IS NULL");
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
    }
}
