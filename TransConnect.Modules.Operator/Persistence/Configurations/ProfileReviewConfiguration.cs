using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Operator.Entities;

namespace TransConnect.Modules.Operator.Persistence.Configurations;

public sealed class ProfileReviewConfiguration : IEntityTypeConfiguration<ProfileReview>
{
    public void Configure(EntityTypeBuilder<ProfileReview> builder)
    {
        builder.ToTable("ProfileReviews");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SubjectType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.HasIndex(x => new { x.SubjectType, x.SubjectId, x.CreatedAt });
    }
}
