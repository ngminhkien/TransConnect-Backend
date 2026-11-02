using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Operator.Entities;

namespace TransConnect.Modules.Operator.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles", t => t.HasCheckConstraint("CK_Vehicles_ProfileCapacity",
            "([VehicleKind] IS NULL AND [SeatCount] IS NULL AND [PermittedPayloadKg] IS NULL) OR " +
            "([VehicleKind] IS NOT NULL AND [VehicleKind] = 'Passenger' AND [SeatCount] IS NOT NULL AND [SeatCount] BETWEEN 1 AND 1000 AND [PermittedPayloadKg] IS NULL) OR " +
            "([VehicleKind] IS NOT NULL AND [VehicleKind] = 'Freight' AND [PermittedPayloadKg] IS NOT NULL AND CAST([PermittedPayloadKg] AS decimal(12,2)) > 0 AND CAST([PermittedPayloadKg] AS decimal(12,2)) <= 1000000 AND [SeatCount] IS NULL)"));
        builder.Property(x => x.VehicleKind).HasMaxLength(20);
        builder.Property(x => x.PermittedPayloadKg).HasPrecision(12, 2);
        builder.Property(x => x.RegistrationPlate).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => x.RegistrationPlate).IsUnique();
    }
}
