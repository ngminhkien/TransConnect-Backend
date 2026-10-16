using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TransConnect.Modules.Notification.Entities;

namespace TransConnect.Modules.Notification.Persistence.Configurations;

public class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        // NotificationMessage chỉ dùng lại code C#; mỗi kênh map bảng độc lập.
        // Không đưa base vào EF hierarchy để tránh thêm discriminator/bảng chung.
        builder.HasBaseType((Type?)null);
        builder.ToTable("EmailMessages");

        builder.Property(e => e.ToEmail).IsRequired().HasMaxLength(256);
        builder.Property(e => e.ToName).HasMaxLength(200);
        builder.Property(e => e.Subject).IsRequired().HasMaxLength(300);
        builder.Property(e => e.TemplateCode).IsRequired().HasMaxLength(100);

        // Không giới hạn độ dài -> nvarchar(max), vì thân email HTML dễ vượt 4000 ký tự.
        builder.Property(e => e.BodyHtml).IsRequired();
        builder.Property(e => e.BodyText).IsRequired();

        builder.Property(e => e.LastError).HasMaxLength(2000);

        builder.Property(e => e.Status).HasConversion<int>();

        // Đúng bộ cột worker lọc mỗi vòng lặp. Thiếu index này thì khi bảng lớn dần,
        // mỗi lần poll sẽ quét toàn bảng.
        builder.HasIndex(e => new { e.Status, e.ScheduledAt });
    }
}
