using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Transport;
using TransConnect.Modules.Compliance.Dto.Requests;
using TransConnect.Modules.Compliance.Entities;
using TransConnect.Modules.Compliance.Options;
using TransConnect.Modules.Compliance.Persistence;
using TransConnect.Modules.Compliance.Services;
using TransConnect.Modules.Compliance.Storage;
using TransConnect.Modules.Operator.Entities;
using TransConnect.Modules.Operator.Persistence;
using TransConnect.Modules.Operator.Services;
using Xunit;

namespace TransConnect.Tests;

public sealed class ProfileDocumentTests
{
    [Fact]
    public async Task TC_PROFILE_DOC_001_Vehicle_capacity_constraint_accepts_fractional_kg_and_rejects_untyped_capacity()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        using var db = new OperatorDbContext(new DbContextOptionsBuilder<OperatorDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        db.Vehicles.Add(new Vehicle { RegistrationPlate = "51A12345", VehicleKind = "Freight", PermittedPayloadKg = 2500.50m });
        await db.SaveChangesAsync();
        db.Vehicles.Add(new Vehicle { RegistrationPlate = "51A12346", PermittedPayloadKg = 100m });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task TC_PROFILE_DOC_002_Resubmission_preserves_old_versions_and_legacy_endpoint_cannot_access_bound_files()
    {
        using var f = new Fixture();
        var first = await f.Service.UploadAsync("Driver", f.Driver.Id, "DriverLicense", Upload(), default);
        await f.Service.SimulateAsync(first.File.Id, new() { Scenario = "Success" }, default);
        await f.Service.ReviewAsync(first.File.Id, new() { Approved = true }, default);
        var second = await f.Service.UploadAsync("Driver", f.Driver.Id, "DriverLicense", Upload("%PDF-1.4 second synthetic"), default);
        Assert.Equal(first.ProfileDocumentId, second.ProfileDocumentId);
        Assert.Equal(1, first.VersionNumber);
        Assert.Equal(2, second.VersionNumber);
        Assert.NotEqual(first.File.Sha256, second.File.Sha256);
        var versions = await f.Service.ListAsync("Driver", f.Driver.Id, "DriverLicense", default);
        Assert.Equal("DemoApproved", versions.Single(x => x.VersionNumber == 1).File.Status);
        Assert.All(versions, x => Assert.Equal("NOT_VERIFIED", x.File.Verification));
        Assert.Equal("Approved", (await f.Operator.Drivers.AsNoTracking().SingleAsync()).ReviewStatus);
        Assert.Empty(await f.Legacy.ListAsync(default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Legacy.GetAsync(first.File.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Legacy.DeleteAsync(first.File.Id, default));
        await f.Service.DeleteAsync(second.File.Id, default);
        var third = await f.Service.UploadAsync("Driver", f.Driver.Id, "DriverLicense", Upload(), default);
        Assert.Equal(3, third.VersionNumber);
        Assert.Equal(2, f.Storage.Objects.Count);
        Assert.Equal(2, (await f.Service.ListAsync("Driver", f.Driver.Id, "DriverLicense", default)).Count);
    }

    [Fact]
    public async Task TC_PROFILE_DOC_003_Driver_documents_are_readable_by_current_owner_but_only_driver_can_write()
    {
        using var f = new Fixture();
        var version = await f.Service.UploadAsync("Driver", f.Driver.Id, "DriverLicense", Upload(), default);
        await f.Service.SimulateAsync(version.File.Id, new() { Scenario = "Success" }, default);
        f.Actor.UserId = f.OwnerA;
        Assert.Single(await f.Service.ListAsync("Driver", f.Driver.Id, "DriverLicense", default));
        Assert.NotEmpty((await f.Service.DownloadAsync(version.File.Id, default)).Content);
        await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Service.ReviewAsync(version.File.Id, new() { Approved = true }, default));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Service.DeleteAsync(version.File.Id, default));
        foreach (var actor in new[] { f.Manager, f.OwnerB, Guid.NewGuid() })
        {
            f.Actor.UserId = actor;
            await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Service.DownloadAsync(version.File.Id, default));
        }
        f.Actor.UserId = f.OwnerA;
        f.Engagement.EndedAt = DateTime.UtcNow;
        await f.Operator.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Service.DownloadAsync(version.File.Id, default));
        f.Actor.UserId = f.Driver.AccountId;
        Assert.NotEmpty((await f.Service.DownloadAsync(version.File.Id, default)).Content);
    }

    [Fact]
    public async Task TC_PROFILE_DOC_004_Vehicle_versions_are_scoped_to_the_operating_organization_and_do_not_follow_the_uploader()
    {
        using var f = new Fixture();
        f.Actor.UserId = f.OwnerA;
        var a = await f.Service.UploadAsync("Vehicle", f.Vehicle.Id, "VehicleRegistration", Upload(), default);
        await f.Service.SimulateAsync(a.File.Id, new() { Scenario = "Success" }, default);
        f.Link.EndedAt = DateTime.UtcNow;
        await f.Operator.SaveChangesAsync();
        f.Operator.OrganizationVehicles.Add(new OrganizationVehicle
        { OrganizationId = f.OrgB.Id, VehicleId = f.Vehicle.Id, VerifiedBy = f.OwnerB, VerificationReference = "Synthetic transfer" });
        await f.Operator.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Service.DownloadAsync(a.File.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Legacy.GetAsync(a.File.Id, default));
        f.Actor.UserId = f.OwnerB;
        Assert.Empty(await f.Service.ListAsync("Vehicle", f.Vehicle.Id, "VehicleRegistration", default));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Service.DownloadAsync(a.File.Id, default));
        var b = await f.Service.UploadAsync("Vehicle", f.Vehicle.Id, "VehicleRegistration", Upload(), default);
        Assert.NotEqual(a.ProfileDocumentId, b.ProfileDocumentId);
        Assert.Equal(1, b.VersionNumber);
        Assert.Equal(2, f.Storage.Objects.Count);
    }

    [Theory]
    [InlineData("Driver", "IdentityCard")]
    [InlineData("Driver", "VehicleRegistration")]
    [InlineData("Vehicle", "DriverLicense")]
    [InlineData("Organization", "BusinessLicense")]
    public async Task TC_PROFILE_DOC_005_Only_two_selected_demo_types_are_accepted(string subjectType, string documentType)
    {
        using var f = new Fixture();
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.UploadAsync(subjectType, f.Driver.Id, documentType, Upload(), default));
        Assert.Empty(f.Storage.Objects);
    }

    [Fact]
    public async Task TC_PROFILE_DOC_006_Storage_failure_preserves_failed_version_for_safe_delete_or_retry()
    {
        using var f = new Fixture();
        f.Storage.Fail = true;
        await Assert.ThrowsAsync<ServiceUnavailableAppException>(() => f.Service.UploadAsync("Driver", f.Driver.Id, "DriverLicense", Upload(), default));
        var failed = Assert.Single(await f.Service.ListAsync("Driver", f.Driver.Id, "DriverLicense", default));
        Assert.Equal("UploadFailed", failed.File.Status);
        Assert.Equal(1, failed.VersionNumber);
        f.Storage.Fail = false;
        var retry = await f.Service.UploadAsync("Driver", f.Driver.Id, "DriverLicense", Upload(), default);
        Assert.Equal(2, retry.VersionNumber);
        await f.Service.DeleteAsync(failed.File.Id, default);
        Assert.Single(await f.Service.ListAsync("Driver", f.Driver.Id, "DriverLicense", default));
    }

    [Fact]
    public async Task TC_PROFILE_DOC_007_Profile_files_keep_synthetic_consent_and_content_validation()
    {
        using var f = new Fixture();
        var invalid = Upload();
        invalid.SyntheticDataConfirmed = false;
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.UploadAsync("Driver", f.Driver.Id, "DriverLicense", invalid, default));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.UploadAsync("Driver", f.Driver.Id, "DriverLicense", Upload("bad"), default));
        Assert.Empty(f.Storage.Objects);
        Assert.Empty(await f.Db.DemoDocuments.ToListAsync());
    }

    [Fact]
    public async Task TC_PROFILE_DOC_008_Concurrent_uploads_reserve_distinct_monotonic_versions_across_contexts()
    {
        var path = Path.Combine(Path.GetTempPath(), "profile-versions-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var options = new DbContextOptionsBuilder<ComplianceDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            using var first = new ComplianceDbContext(options);
            using var second = new ComplianceDbContext(options);
            await first.Database.EnsureCreatedAsync();
            var subjectId = Guid.NewGuid();
            first.DemoProfileDocuments.Add(new DemoProfileDocument { SubjectType = "Driver", SubjectId = subjectId, DocumentType = "DriverLicense" });
            await first.SaveChangesAsync();
            var actor = new ComplianceDemoTests.Actor();
            var accounts = new ComplianceDemoTests.Accounts();
            var storage = new Storage();
            ProfileDocumentService Create(ComplianceDbContext db) => new(db,
                new DemoDocumentService(db, storage, actor, accounts, Options.Create(new ComplianceDemoOptions { Enabled = true }),
                    new ComplianceDemoTests.DemoEnvironment()), new AllowAccess(actor.UserId!.Value));
            var responses = await Task.WhenAll(
                Task.Run(() => Create(first).UploadAsync("Driver", subjectId, "DriverLicense", Upload(), default)),
                Task.Run(() => Create(second).UploadAsync("Driver", subjectId, "DriverLicense", Upload(), default)));
            Assert.Equal(new[] { 1, 2 }, responses.Select(x => x.VersionNumber).Order().ToArray());
            Assert.Equal(2, (await first.DemoProfileDocuments.AsNoTracking().SingleAsync()).LastVersionNumber);
            Assert.Equal(2, storage.Objects.Count);
        }
        finally { foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix); }
    }

    [Fact]
    public void TC_PROFILE_DOC_009_Profile_migrations_have_no_model_drift_and_do_not_backfill_personal_data_or_change_reviews()
    {
        const string connection = "Server=unused;Database=ScriptOnly;Integrated Security=true";
        using var db = new OperatorDbContext(new DbContextOptionsBuilder<OperatorDbContext>().UseSqlServer(connection).Options);
        using var compliance = new ComplianceDbContext(new DbContextOptionsBuilder<ComplianceDbContext>().UseSqlServer(connection).Options);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.False(compliance.Database.HasPendingModelChanges());
        var sql = db.GetService<IMigrator>().GenerateScript("20261019112631_AuditContextAndRetention",
            "20261019164634_MinimalProfilesAndEngagementSnapshots");
        Assert.Contains("[DriverNameSnapshot] nvarchar(200) NULL", sql);
        Assert.Contains("[ContactPhone] nvarchar(20) NULL", sql);
        Assert.Contains("[PermittedPayloadKg] decimal(12,2) NULL", sql);
        Assert.DoesNotContain("UPDATE", sql);
        Assert.Contains("DemoProfileDocuments", compliance.GetService<IMigrator>().GenerateScript());
    }

    private static UploadDemoDocumentRequest Upload(string text = "%PDF-1.4 SYNTHETIC ONLY") => new()
    {
        SyntheticDataConfirmed = true,
        File = new FormFile(new MemoryStream(Encoding.UTF8.GetBytes(text)), 0, Encoding.UTF8.GetByteCount(text), "File", "demo.pdf")
    };
    private sealed class AllowAccess(Guid actor) : IProfileDocumentAccess
    {
        public Task<ProfileDocumentScope> AuthorizeAsync(string subjectType, Guid subjectId, bool write, CancellationToken ct) =>
            Task.FromResult(new ProfileDocumentScope(actor, null));
    }
    private sealed class Storage : IPrivateDocumentStorage
    {
        public readonly ConcurrentDictionary<string, byte[]> Objects = new();
        public bool Fail { get; set; }
        public Task PutAsync(string bucket, string key, byte[] content, string contentType, CancellationToken ct)
        { if (Fail) throw new ServiceUnavailableAppException("Synthetic storage outage"); Objects[bucket + key] = content; return Task.CompletedTask; }
        public Task<byte[]> ReadAsync(string bucket, string key, int maxBytes, CancellationToken ct) => Task.FromResult(Objects[bucket + key]);
        public Task DeleteAsync(string bucket, string key, CancellationToken ct) { Objects.TryRemove(bucket + key, out _); return Task.CompletedTask; }
    }
    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _operator = new("Data Source=:memory:");
        private readonly SqliteConnection _compliance = new("Data Source=:memory:");
        public readonly ComplianceDemoTests.Actor Actor = new();
        public readonly ComplianceDemoTests.Accounts Accounts = new();
        public readonly Guid OwnerA = Guid.NewGuid(), OwnerB = Guid.NewGuid(), Manager = Guid.NewGuid();
        public readonly Organization OrgA = new() { Name = "A", RegistrationNumber = "A", ProposedOwnerEmail = "a@example.com", ReviewStatus = "Approved" };
        public readonly Organization OrgB = new() { Name = "B", RegistrationNumber = "B", ProposedOwnerEmail = "b@example.com", ReviewStatus = "Approved" };
        public readonly Vehicle Vehicle = new() { RegistrationPlate = "51A001" };
        public Driver Driver { get; }
        public DriverEngagement Engagement { get; }
        public OrganizationVehicle Link { get; }
        public readonly Storage Storage = new();
        public OperatorDbContext Operator { get; }
        public ComplianceDbContext Db { get; }
        public DemoDocumentService Legacy { get; }
        public ProfileDocumentService Service { get; }
        public Fixture()
        {
            _operator.Open(); _compliance.Open();
            Operator = new(new DbContextOptionsBuilder<OperatorDbContext>().UseSqlite(_operator).Options);
            Db = new(new DbContextOptionsBuilder<ComplianceDbContext>().UseSqlite(_compliance).Options);
            Operator.Database.EnsureCreated(); Db.Database.EnsureCreated();
            Driver = new() { AccountId = Actor.UserId!.Value, FullName = "Synthetic Driver", ReviewStatus = "Approved" };
            Engagement = new() { OrganizationId = OrgA.Id, DriverId = Driver.Id, AcceptedAt = DateTime.UtcNow,
                TokenHash = "engagement", ExpiresAt = DateTime.UtcNow.AddDays(1) };
            Link = new() { OrganizationId = OrgA.Id, VehicleId = Vehicle.Id, VerifiedBy = OwnerA, VerificationReference = "Synthetic" };
            Operator.AddRange(OrgA, OrgB, Driver, Vehicle, Engagement, Link,
                new OrganizationMember { OrganizationId = OrgA.Id, AccountId = OwnerA, Role = "OrganizationOwner" },
                new OrganizationMember { OrganizationId = OrgB.Id, AccountId = OwnerB, Role = "OrganizationOwner" },
                new OrganizationMember { OrganizationId = OrgA.Id, AccountId = Manager, Role = "Manager" });
            Operator.SaveChanges();
            Legacy = new(Db, Storage, Actor, Accounts, Options.Create(new ComplianceDemoOptions { Enabled = true }),
                new ComplianceDemoTests.DemoEnvironment());
            Service = new(Db, Legacy, new ProfileDocumentAccess(Operator, new OperatorAccess(Operator, Actor, Accounts)));
        }
        public void Dispose() { Operator.Dispose(); Db.Dispose(); _operator.Dispose(); _compliance.Dispose(); }
    }
}
