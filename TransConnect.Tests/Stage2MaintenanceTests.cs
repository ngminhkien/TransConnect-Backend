using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Audit;
using TransConnect.Common.Maintenance;
using TransConnect.Common.Notifications;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Enums;
using TransConnect.Modules.Identity.Options;
using TransConnect.Modules.Identity.Persistence;
using TransConnect.Modules.Identity.Services;
using TransConnect.Modules.Notification.Entities;
using TransConnect.Modules.Notification.Enums;
using TransConnect.Modules.Notification.Options;
using TransConnect.Modules.Notification.Persistence;
using TransConnect.Modules.Notification.Providers;
using TransConnect.Modules.Notification.Services;
using TransConnect.Modules.Operator.Entities;
using TransConnect.Modules.Operator.Persistence;
using TransConnect.Modules.Operator.Services;
using Xunit;

namespace TransConnect.Tests;

public sealed class Stage2MaintenanceTests
{
    [Fact]
    public void Stage_two_migrations_match_all_models_and_generate_audit_and_cleanup_sql()
    {
        const string connection = "Server=unused;Database=ScriptOnly;Integrated Security=true";
        using var identity = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(connection).Options);
        using var operators = new OperatorDbContext(new DbContextOptionsBuilder<OperatorDbContext>().UseSqlServer(connection).Options);
        using var mail = new NotificationDbContext(new DbContextOptionsBuilder<NotificationDbContext>().UseSqlServer(connection).Options);
        Assert.False(identity.Database.HasPendingModelChanges());
        Assert.False(operators.Database.HasPendingModelChanges());
        Assert.False(mail.Database.HasPendingModelChanges());
        Assert.Contains("CREATE TABLE [identity].[RbacAuditLogs]", identity.GetService<IMigrator>().GenerateScript());
        Assert.Contains("[OrganizationId] uniqueidentifier NULL", operators.GetService<IMigrator>().GenerateScript());
        var sql = mail.GetService<IMigrator>().GenerateScript();
        Assert.Contains("[DispatchLease] uniqueidentifier NULL", sql);
        Assert.Contains("[RetentionCategory] = CASE", sql);
        Assert.Contains("[BodyHtml] = N'', [BodyText] = N''", sql);
    }

    [Fact]
    public async Task Independent_dispatchers_do_not_send_a_message_while_another_lease_is_active()
    {
        var path = Path.Combine(Path.GetTempPath(), "mail-race-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var options = new DbContextOptionsBuilder<NotificationDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            using var first = new NotificationDbContext(options);
            using var second = new NotificationDbContext(options);
            await first.Database.EnsureCreatedAsync();
            var clock = new TestClock();
            var now = clock.GetUtcNow().UtcDateTime;
            first.EmailMessages.Add(new EmailMessage { ToEmail = "race@example.com", Subject = "Subject",
                BodyText = "SECRET", BodyHtml = "SECRET", TemplateCode = "transaction-notice", ScheduledAt = now, CreatedAt = now });
            await first.SaveChangesAsync();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var provider = new RecordingProvider { OnSend = async () => { entered.SetResult(); await release.Task; } };
            EmailDispatcher Sender(NotificationDbContext db) => new(db, provider, [], Options.Create(new EmailOptions()),
                clock, NullLogger<EmailDispatcher>.Instance);
            var firstSend = Sender(first).ProcessBatchAsync(default);
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await Sender(second).ProcessBatchAsync(default);
                Assert.Equal(1, provider.Deliveries);
            }
            finally { release.TrySetResult(); await firstSend; }
            Assert.Equal(NotificationStatus.Sent, (await second.EmailMessages.AsNoTracking().SingleAsync()).Status);
        }
        finally { foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix); }
    }

    [Fact]
    public async Task Cleanup_also_removes_bodies_of_soft_deleted_email_rows()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var message = app.Message();
        message.DeletedAt = app.Now;
        app.Mail.Add(message);
        await app.Mail.SaveChangesAsync();
        await app.Retention().ExecuteAsync(default);
        var saved = await app.Mail.EmailMessages.IgnoreQueryFilters().AsNoTracking().SingleAsync();
        Assert.Equal(NotificationStatus.Failed, saved.Status);
        Assert.Empty(saved.BodyText);
        Assert.NotNull(saved.BodyPurgedAt);
    }

    [Fact]
    public async Task Rbac_audit_is_allowlisted_has_request_context_and_rolls_back_with_the_change()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var role = new Role { Name = "Dispatcher", Description = "Public description" };
        var user = new User { Email = "audit@example.com", FullName = "SecretName", PasswordHash = "SECRET-HASH" };
        app.Identity.AddRange(role, user);
        await app.Identity.SaveChangesAsync();
        app.Identity.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        await app.Identity.SaveChangesAsync();
        role.Name = "Manager";
        await app.Identity.SaveChangesAsync();
        var logs = await app.Identity.RbacAuditLogs.AsNoTracking().ToListAsync();
        Assert.Equal(3, logs.Count);
        Assert.All(logs, x => { Assert.Equal(app.Audit.ActorAccountId, x.ActorAccountId); Assert.Equal("request-test", x.RequestId); });
        Assert.Contains(logs, x => x.SubjectType == "UserRole" && x.ChangesJson.Contains(user.Id.ToString()));
        var update = Assert.Single(logs.Where(x => x.Action == "Modified"));
        Assert.Contains("Dispatcher", update.ChangesJson);
        Assert.Contains("Manager", update.ChangesJson);
        Assert.DoesNotContain("PasswordHash", string.Join("", logs.Select(x => x.ChangesJson)));
        Assert.DoesNotContain("SECRET-HASH", string.Join("", logs.Select(x => x.ChangesJson)));

        await using (var tx = await app.Identity.Database.BeginTransactionAsync())
        {
            app.Identity.Roles.Add(new Role { Name = "Rollback" });
            await app.Identity.SaveChangesAsync();
            await tx.RollbackAsync();
        }
        app.Identity.ChangeTracker.Clear();
        Assert.False(await app.Identity.Roles.AnyAsync(x => x.Name == "Rollback"));
        Assert.Equal(3, await app.Identity.RbacAuditLogs.CountAsync());
        app.Identity.Roles.Add(new Role { Name = "Manager" });
        await Assert.ThrowsAsync<DbUpdateException>(() => app.Identity.SaveChangesAsync());
        Assert.Equal(3, await app.Identity.RbacAuditLogs.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task Physical_user_deletion_is_rejected_and_soft_deleted_accounts_are_not_orphans()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var user = new User { Email = "deleted@example.com", FullName = "User", IsActive = false, DeletedAt = app.Now };
        app.Identity.Users.Add(user);
        app.Identity.SaveChanges();
        var directory = new AccountReferenceDirectory(app.Identity);
        var missingId = Guid.NewGuid();
        Assert.Equal(new[] { missingId }, (await directory.FindMissingAsync([user.Id, missingId], default)).ToArray());
        app.Identity.Users.Remove(user);
        Assert.Throws<ConflictException>(() => app.Identity.SaveChanges());
        Assert.True(await app.Identity.Users.IgnoreQueryFilters().AsNoTracking().AnyAsync(x => x.Id == user.Id));
    }

    [Fact]
    public async Task Both_audit_stores_purge_only_rows_older_than_six_calendar_months_and_respect_batch_size()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var cutoff = app.Now.AddMonths(-6);
        foreach (var date in new[] { cutoff.AddTicks(-1), cutoff.AddDays(-1), cutoff, app.Now })
        {
            app.Identity.RbacAuditLogs.Add(new RbacAuditLog { CreatedAt = date });
            app.Operator.AuditLogs.Add(new OperatorAuditLog { CreatedAt = date, OrganizationId = null });
        }
        await app.Identity.SaveChangesAsync();
        await app.Operator.SaveChangesAsync();
        var options = Options.Create(new MaintenanceOptions { BatchSize = 1 });
        var identityTask = new IdentityAuditRetentionTask(app.Identity, options, app.Clock);
        var operatorTask = new OperatorAuditRetentionTask(app.Operator, options, app.Clock);
        await identityTask.ExecuteAsync(default);
        await operatorTask.ExecuteAsync(default);
        Assert.Equal(3, await app.Identity.RbacAuditLogs.CountAsync());
        Assert.Equal(3, await app.Operator.AuditLogs.CountAsync());
        await identityTask.ExecuteAsync(default);
        await operatorTask.ExecuteAsync(default);
        Assert.Equal(2, await app.Identity.RbacAuditLogs.CountAsync());
        Assert.Equal(2, await app.Operator.AuditLogs.CountAsync());
        Assert.All(await app.Identity.RbacAuditLogs.ToListAsync(), x => Assert.True(x.CreatedAt >= cutoff));
    }

    [Fact]
    public async Task Orphan_check_reports_missing_references_without_modifying_profiles()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var missing = Guid.NewGuid();
        app.Operator.Drivers.Add(new Driver { AccountId = missing, FullName = "Orphan" });
        await app.Operator.SaveChangesAsync();
        var logger = new CapturingLogger<AccountReferenceCheckTask>();
        var task = new AccountReferenceCheckTask(app.Operator, new AccountReferenceDirectory(app.Identity),
            Options.Create(new MaintenanceOptions { BatchSize = 1 }), logger);
        await task.ExecuteAsync(default);
        Assert.Contains(logger.Messages, x => x.Contains(missing.ToString()));
        Assert.Equal(1, await app.Operator.Drivers.CountAsync());
        Assert.Null((await app.Operator.Drivers.SingleAsync()).DeletedAt);
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("used", false)]
    [InlineData("expired", false)]
    [InlineData("attempts", false)]
    [InlineData("inactive-account", false)]
    public async Task Otp_guard_reads_current_source_and_account_state(string state, bool expected)
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var user = new User { Email = "otp@example.com", FullName = "OTP", IsActive = state != "inactive-account" };
        var code = new VerificationCode { User = user, CodeHash = "HASH", Purpose = VerificationPurpose.PasswordReset,
            ExpiresAt = state == "expired" ? app.Now : app.Now.AddMinutes(5), IsUsed = state == "used",
            AttemptCount = state == "attempts" ? 5 : 0 };
        app.Identity.VerificationCodes.Add(code);
        await app.Identity.SaveChangesAsync();
        var guard = new VerificationEmailGuard(app.Identity, Options.Create(new VerificationOptions { MaxAttempts = 5 }), app.Clock);
        Assert.Equal(expected, await guard.CanDeliverAsync(code.Id, user.Email, default));
        Assert.False(await guard.CanDeliverAsync(code.Id, "other@example.com", default));
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("revoked", false)]
    [InlineData("accepted", false)]
    [InlineData("expired", false)]
    public async Task Invitation_guard_checks_revocation_consent_and_expiry(string state, bool expected)
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var org = new Organization { Name = "Org", RegistrationNumber = "TEST", ProposedOwnerEmail = "owner@example.com", ReviewStatus = "Approved" };
        app.Operator.Organizations.Add(org);
        var invitation = new OrganizationInvitation { OrganizationId = org.Id, Email = "invite@example.com", Role = "Driver",
            TokenHash = "hash", ExpiresAt = state == "expired" ? app.Now : app.Now.AddDays(1),
            RevokedAt = state == "revoked" ? app.Now : null, AcceptedAt = state == "accepted" ? app.Now : null };
        app.Operator.Invitations.Add(invitation);
        await app.Operator.SaveChangesAsync();
        var guard = new InvitationEmailGuard(app.Operator, app.Clock);
        Assert.Equal(expected, await guard.CanDeliverAsync(invitation.Id, invitation.Email, default));
        Assert.False(await guard.CanDeliverAsync(invitation.Id, "someone@example.com", default));
    }

    [Fact]
    public async Task Successful_delivery_purges_bodies_immediately_and_keeps_terminal_metadata()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var message = app.Message();
        app.Mail.Add(message);
        await app.Mail.SaveChangesAsync();
        var provider = new RecordingProvider();
        await app.Dispatcher(provider).ProcessBatchAsync(default);
        var saved = await app.Mail.EmailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(1, provider.Deliveries);
        Assert.Equal(NotificationStatus.Sent, saved.Status);
        Assert.Equal("", saved.BodyHtml);
        Assert.Equal("", saved.BodyText);
        Assert.Equal(app.Now, saved.TerminalAt);
        Assert.Equal(app.Now, saved.BodyPurgedAt);
        Assert.Null(saved.DispatchLease);
    }

    [Fact]
    public async Task Retry_rechecks_source_and_never_retains_provider_secrets_in_error_metadata()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var guard = new SwitchGuard();
        var message = app.Message("Otp");
        message.SourceType = guard.SourceType;
        message.SourceId = Guid.NewGuid();
        message.DeliverBefore = app.Now.AddMinutes(5);
        app.Mail.Add(message);
        await app.Mail.SaveChangesAsync();
        var provider = new RecordingProvider { Error = new InvalidOperationException("SECRET-OTP-and-URL") };
        await app.Dispatcher(provider, [guard]).ProcessBatchAsync(default);
        var pending = await app.Mail.EmailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(NotificationStatus.Pending, pending.Status);
        Assert.Equal("InvalidOperationException", pending.LastError);
        Assert.NotEmpty(pending.BodyText);
        guard.Active = false;
        app.Clock.Advance(TimeSpan.FromMinutes(1));
        await app.Dispatcher(provider, [guard]).ProcessBatchAsync(default);
        var failed = await app.Mail.EmailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(1, provider.Deliveries);
        Assert.Equal(NotificationStatus.Failed, failed.Status);
        Assert.Empty(failed.BodyText);
        Assert.Equal(app.Clock.GetUtcNow().UtcDateTime, failed.TerminalAt);
    }

    [Fact]
    public async Task Permanent_failure_after_max_attempts_purges_body_and_cancellation_releases_lease()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var message = app.Message();
        app.Mail.Add(message);
        await app.Mail.SaveChangesAsync();
        using var cancellation = new CancellationTokenSource();
        var provider = new RecordingProvider { OnSend = () => { cancellation.Cancel(); return Task.FromCanceled(cancellation.Token); } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => app.Dispatcher(provider).ProcessBatchAsync(cancellation.Token));
        var pending = await app.Mail.EmailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(NotificationStatus.Pending, pending.Status);
        Assert.Null(pending.DispatchLease);
        Assert.NotEmpty(pending.BodyText);
        provider.OnSend = null;
        provider.Error = new InvalidOperationException("SECRET");
        await app.Dispatcher(provider, maxAttempts: 1).ProcessBatchAsync(default);
        var failed = await app.Mail.EmailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(NotificationStatus.Failed, failed.Status);
        Assert.Empty(failed.BodyHtml);
        Assert.Equal("InvalidOperationException", failed.LastError);
    }

    [Fact]
    public async Task Sender_cannot_overwrite_state_after_losing_its_lease()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var message = app.Message();
        app.Mail.Add(message);
        await app.Mail.SaveChangesAsync();
        var otherLease = Guid.NewGuid();
        var provider = new RecordingProvider { OnSend = async () =>
            await app.Mail.EmailMessages.Where(x => x.Id == message.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.DispatchLease, otherLease)) };
        await app.Dispatcher(provider).ProcessBatchAsync(default);
        var saved = await app.Mail.EmailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(NotificationStatus.Sending, saved.Status);
        Assert.Equal(otherLease, saved.DispatchLease);
        Assert.NotEmpty(saved.BodyText);
    }

    [Fact]
    public async Task Retention_preserves_sending_rows_and_exact_seven_thirty_day_boundaries()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        foreach (var (category, days) in new[] { ("Otp", 7), ("Invitation", 30), ("General", 30) })
        {
            var boundary = app.Message(category);
            boundary.Status = NotificationStatus.Failed;
            boundary.TerminalAt = app.Now.AddDays(-days);
            boundary.BodyPurgedAt = boundary.TerminalAt;
            boundary.BodyText = boundary.BodyHtml = "";
            var expired = app.Message(category);
            expired.Status = NotificationStatus.Failed;
            expired.TerminalAt = boundary.TerminalAt.Value.AddSeconds(-1);
            expired.BodyPurgedAt = expired.TerminalAt;
            expired.BodyText = expired.BodyHtml = "";
            app.Mail.AddRange(boundary, expired);
        }
        var sending = app.Message("Otp");
        sending.Status = NotificationStatus.Sending;
        sending.CreatedAt = app.Now.AddDays(-100);
        app.Mail.Add(sending);
        await app.Mail.SaveChangesAsync();
        await app.Retention().ExecuteAsync(default);
        Assert.Equal(4, await app.Mail.EmailMessages.CountAsync());
        Assert.NotEmpty((await app.Mail.EmailMessages.AsNoTracking().SingleAsync(x => x.Id == sending.Id)).BodyText);
        app.Clock.Advance(TimeSpan.FromSeconds(1));
        await app.Retention().ExecuteAsync(default);
        Assert.Equal(1, await app.Mail.EmailMessages.CountAsync());
    }

    [Fact]
    public async Task Cleanup_terminates_future_retry_for_invalid_source_and_24_hour_general_message()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var otp = app.Message("Otp");
        otp.NextAttemptAt = app.Now.AddDays(1);
        otp.SourceType = "missing-handler";
        otp.SourceId = Guid.NewGuid();
        var old = app.Message();
        old.CreatedAt = app.Now.AddHours(-24);
        old.ScheduledAt = app.Now.AddDays(1);
        app.Mail.AddRange(otp, old);
        await app.Mail.SaveChangesAsync();
        await app.Retention().ExecuteAsync(default);
        Assert.All(await app.Mail.EmailMessages.AsNoTracking().ToListAsync(), x =>
        { Assert.Equal(NotificationStatus.Failed, x.Status); Assert.Empty(x.BodyText); Assert.NotNull(x.TerminalAt); });
    }

    [Fact]
    public async Task File_sink_removes_its_copy_and_cleanup_leaves_unrelated_files_untouched()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        Directory.CreateDirectory(app.Directory);
        var provider = new FileEmailProvider(app.Files, NullLogger<FileEmailProvider>.Instance);
        await provider.SendAsync(new OutgoingEmail("recipient@example.com", null, "Subject", "SECRET", "SECRET"));
        Assert.Empty(Directory.GetFiles(app.Directory));
        await File.WriteAllTextAsync(Path.Combine(app.Directory, "20261019-180000-001_qa@example.com.html"), "SECRET");
        await File.WriteAllTextAsync(Path.Combine(app.Directory, "notes.html"), "Do not remove");
        await app.Files.DeleteLegacyCopiesAsync(default);
        Assert.Equal(new[] { "notes.html" }, Directory.GetFiles(app.Directory).Select(Path.GetFileName).ToArray());
    }

    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _identity = new("Data Source=:memory:");
        private readonly SqliteConnection _operator = new("Data Source=:memory:");
        private readonly SqliteConnection _mail = new("Data Source=:memory:");
        public readonly TestClock Clock = new();
        public DateTime Now => Clock.GetUtcNow().UtcDateTime;
        public readonly TestAuditContext Audit = new();
        public readonly string Directory = Path.Combine(Path.GetTempPath(), "transconnect-stage2-" + Guid.NewGuid().ToString("N"));
        public IdentityDbContext Identity { get; private set; } = null!;
        public OperatorDbContext Operator { get; private set; } = null!;
        public NotificationDbContext Mail { get; private set; } = null!;
        public EmailFileCleanup Files { get; private set; } = null!;
        public async Task InitializeAsync()
        {
            await _identity.OpenAsync(); await _operator.OpenAsync(); await _mail.OpenAsync();
            Identity = new(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlite(_identity).Options, Audit);
            Operator = new(new DbContextOptionsBuilder<OperatorDbContext>().UseSqlite(_operator).Options);
            Mail = new(new DbContextOptionsBuilder<NotificationDbContext>().UseSqlite(_mail).Options);
            await Identity.Database.EnsureCreatedAsync(); await Operator.Database.EnsureCreatedAsync(); await Mail.Database.EnsureCreatedAsync();
            Files = new(Options.Create(new EmailOptions { FileDropPath = Directory }), new TestEnvironment { ContentRootPath = Directory });
        }
        public EmailMessage Message(string category = "General") => new()
        {
            ToEmail = "recipient@example.com", Subject = "Subject", TemplateCode = category == "Otp" ? "password-reset" : "transaction-notice",
            BodyHtml = "<p>SECRET</p>", BodyText = "SECRET", CreatedAt = Now, ScheduledAt = Now,
            RetentionCategory = category
        };
        public EmailDispatcher Dispatcher(IEmailProvider provider, IEmailDeliveryGuard[]? guards = null, int maxAttempts = 5) =>
            new(Mail, provider, guards ?? [], Options.Create(new EmailOptions { Worker = new EmailWorkerOptions { MaxAttempts = maxAttempts } }),
                Clock, NullLogger<EmailDispatcher>.Instance);
        public EmailRetentionTask Retention(IEmailDeliveryGuard[]? guards = null) =>
            new(Mail, guards ?? [], Options.Create(new MaintenanceOptions()), Clock, Files);
        public void Dispose()
        {
            Identity?.Dispose(); Operator?.Dispose(); Mail?.Dispose();
            _identity.Dispose(); _operator.Dispose(); _mail.Dispose();
            if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, true);
        }
    }
    private sealed class TestAuditContext : IAuditContext
    {
        public Guid? ActorAccountId { get; } = Guid.NewGuid();
        public string? RequestId => "request-test";
    }
    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
    private sealed class SwitchGuard : IEmailDeliveryGuard
    {
        public string SourceType => "test.source";
        public bool Active { get; set; } = true;
        public Task<bool> CanDeliverAsync(Guid sourceId, string recipient, CancellationToken ct) => Task.FromResult(Active);
    }
    private sealed class RecordingProvider : IEmailProvider
    {
        public int Deliveries { get; private set; }
        public Exception? Error { get; set; }
        public Func<Task>? OnSend { get; set; }
        public async Task SendAsync(OutgoingEmail email, CancellationToken cancellationToken = default)
        {
            Deliveries++;
            if (OnSend is not null) await OnSend();
            if (Error is not null) throw Error;
        }
    }
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public readonly List<string> Messages = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
