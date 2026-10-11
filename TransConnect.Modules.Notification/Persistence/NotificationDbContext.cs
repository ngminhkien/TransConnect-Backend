using Microsoft.EntityFrameworkCore;
using TransConnect.Data;
using TransConnect.Modules.Notification.Entities;

namespace TransConnect.Modules.Notification.Persistence;

public class NotificationDbContext(DbContextOptions<NotificationDbContext> options)
    : AppDbContextBase(options)
{
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Schema riêng cho module Notification — xem docs/base-libraries-guidelines.md.
        modelBuilder.HasDefaultSchema("notification");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationDbContext).Assembly);

        // Gọi sau cùng: AppDbContextBase áp soft-delete query filter cho các entity
        // kế thừa BaseEntity, cần chạy sau khi model đã được cấu hình đầy đủ.
        base.OnModelCreating(modelBuilder);
    }
}
