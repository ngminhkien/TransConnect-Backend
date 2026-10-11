using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Identity.Entities;

namespace TransConnect.Modules.Identity.Persistence.Configurations;

public class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> builder)
    {
        builder.ToTable("VerificationCodes");

        builder.Property(v => v.CodeHash).IsRequired().HasMaxLength(200);
        builder.Property(v => v.Purpose).HasConversion<int>();

        builder.HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Đúng bộ cột dùng để tìm mã còn hiệu lực của 1 user cho 1 mục đích.
        builder.HasIndex(v => new { v.UserId, v.Purpose, v.IsUsed });
    }
}
