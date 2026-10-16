using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TransConnect.Modules.Notification.Entities;
using TransConnect.Modules.Notification.Enums;
using TransConnect.Modules.Notification.Persistence;
using Xunit;

namespace TransConnect.Tests;

public class NotificationPersistenceTests
{
    [Fact]
    public void Notification_refactor_preserves_existing_sql_server_schema()
    {
        using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlServer("Server=unused;Database=ScriptOnly;Integrated Security=true").Options);

        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE [notification].[EmailMessages]", script);
        Assert.DoesNotContain("Discriminator", script);
        Assert.DoesNotContain("CREATE TABLE [notification].[NotificationMessages]", script);
    }

    [Theory]
    [InlineData(NotificationStatus.Pending, 0)]
    [InlineData(NotificationStatus.Sending, 1)]
    [InlineData(NotificationStatus.Sent, 2)]
    [InlineData(NotificationStatus.Failed, 3)]
    public async Task Inherited_delivery_fields_round_trip_with_existing_status_values(
        NotificationStatus status, int storedStatus)
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var message = new EmailMessage
        {
            ToEmail = "recipient@example.com",
            Subject = "Test notification",
            BodyHtml = "<p>Test</p>",
            BodyText = "Test",
            TemplateCode = "email-verification",
            Status = status,
            ScheduledAt = now,
            AttemptCount = 2,
            NextAttemptAt = now.AddMinutes(1),
            SentAt = status == NotificationStatus.Sent ? now : null,
            ProcessingStartedAt = now,
            LastError = "Previous delivery attempt failed"
        };
        db.EmailMessages.Add(message);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var saved = await db.EmailMessages.SingleAsync();
        Assert.Equal(status, saved.Status);
        Assert.Equal(message.TemplateCode, saved.TemplateCode);
        Assert.Equal(message.ScheduledAt, saved.ScheduledAt);
        Assert.Equal(message.AttemptCount, saved.AttemptCount);
        Assert.Equal(message.NextAttemptAt, saved.NextAttemptAt);
        Assert.Equal(message.SentAt, saved.SentAt);
        Assert.Equal(message.ProcessingStartedAt, saved.ProcessingStartedAt);
        Assert.Equal(message.LastError, saved.LastError);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Status FROM EmailMessages";
        Assert.Equal(storedStatus, Convert.ToInt32(await command.ExecuteScalarAsync()));

        // Audit và soft-delete vẫn áp dụng qua cả hai lớp kế thừa.
        saved.DeletedAt = now;
        await db.SaveChangesAsync();
        Assert.NotNull(saved.UpdatedAt);
        Assert.Empty(await db.EmailMessages.ToListAsync());
        Assert.Single(await db.EmailMessages.IgnoreQueryFilters().ToListAsync());
    }
}
