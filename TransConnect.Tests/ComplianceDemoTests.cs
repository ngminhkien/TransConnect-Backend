using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Modules.Compliance;
using TransConnect.Modules.Compliance.Dto.Requests;
using TransConnect.Modules.Compliance.Options;
using TransConnect.Modules.Compliance.Persistence;
using TransConnect.Modules.Compliance.Services;
using TransConnect.Modules.Compliance.Storage;
using Xunit;

namespace TransConnect.Tests;

public class ComplianceDemoTests
{
    [Fact]
    public async Task TC_COMPLIANCE_DEMO_001_Upload_simulate_review_download_remain_unverified()
    {
        using var fixture = new Fixture();
        var doc = await fixture.Service.UploadAsync(Upload(), default);
        Assert.Equal("PendingSimulation", doc.Status);
        Assert.Equal("NOT_SCANNED", doc.ScanResult);
        Assert.Single(fixture.Storage.Objects);
        await Assert.ThrowsAsync<ForbiddenAppException>(() => fixture.Service.DownloadAsync(doc.Id, default));
        var simulated = await fixture.Service.SimulateAsync(doc.Id, new() { Scenario = "Success" }, default);
        Assert.Equal("SIMULATED_CLEAN", simulated.ScanResult);
        Assert.Contains("MÔ PHỎNG", simulated.OcrText);
        Assert.Equal("NOT_VERIFIED", simulated.Verification);
        var replay = await fixture.Service.SimulateAsync(doc.Id, new() { Scenario = "Success" }, default);
        Assert.Equal(simulated.SimulatedAt, replay.SimulatedAt);
        var reviewed = await fixture.Service.ReviewAsync(doc.Id, new() { Approved = true }, default);
        Assert.Equal("DemoApproved", reviewed.Status);
        Assert.True(reviewed.IsDemo);
        Assert.Equal("NOT_VERIFIED", reviewed.Verification);
        var download = await fixture.Service.DownloadAsync(doc.Id, default);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(download.Content)), doc.Sha256);
        Assert.EndsWith(".bin", download.FileName);
        await fixture.Service.DeleteAsync(doc.Id, default);
        Assert.Empty(fixture.Storage.Objects);
        Assert.Empty(await fixture.Service.ListAsync(default));
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Service.GetAsync(doc.Id, default));
    }

    [Theory]
    [InlineData("Infected", "DemoBlocked", "SIMULATED_INFECTED")]
    [InlineData("ScannerUnavailable", "DemoFailed", "SIMULATED_UNAVAILABLE")]
    [InlineData("OcrFailed", "DemoFailed", "SIMULATED_CLEAN")]
    public async Task TC_COMPLIANCE_DEMO_002_Failure_scenarios_never_release_files(string scenario, string status, string scan)
    {
        using var f = new Fixture();
        var doc = await f.Service.UploadAsync(Upload(), default);
        var result = await f.Service.SimulateAsync(doc.Id, new() { Scenario = scenario }, default);
        Assert.Equal(status, result.Status);
        Assert.Equal(scan, result.ScanResult);
        Assert.Null(result.OcrText);
        await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Service.DownloadAsync(doc.Id, default));
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.ReviewAsync(doc.Id, new() { Approved = true }, default));
        Assert.Single(f.Storage.Objects);
    }

    [Fact]
    public async Task TC_COMPLIANCE_DEMO_003_Other_accounts_cannot_read_modify_or_delete()
    {
        using var f = new Fixture();
        var doc = await f.Service.UploadAsync(Upload(), default);
        f.Actor.UserId = Guid.NewGuid();
        Assert.Empty(await f.Service.ListAsync(default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.GetAsync(doc.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.SimulateAsync(doc.Id, new(), default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.ReviewAsync(doc.Id, new() { Approved = true }, default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.DownloadAsync(doc.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.DeleteAsync(doc.Id, default));
        f.Accounts.Active = false;
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => f.Service.ListAsync(default));
    }

    [Fact]
    public async Task TC_COMPLIANCE_DEMO_004_Validate_consent_size_and_magic_before_storage()
    {
        using var f = new Fixture();
        var request = Upload();
        request.SyntheticDataConfirmed = false;
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.UploadAsync(request, default));
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.UploadAsync(Upload("not a PDF"), default));
        request = Upload();
        request.File = new FormFile(new MemoryStream(), 0, DemoDocumentService.MaxBytes + 1L, "File", "sample.png");
        await Assert.ThrowsAsync<ValidationAppException>(() => f.Service.UploadAsync(request, default));
        Assert.Empty(f.Storage.Objects);
        Assert.Empty(await f.Db.DemoDocuments.ToListAsync());
    }

    [Fact]
    public async Task TC_COMPLIANCE_DEMO_005_Storage_failure_and_object_tampering_fail_closed()
    {
        using var f = new Fixture();
        f.Storage.Fail = true;
        await Assert.ThrowsAsync<ServiceUnavailableAppException>(() => f.Service.UploadAsync(Upload(), default));
        var failed = Assert.Single(await f.Service.ListAsync(default));
        Assert.Equal("UploadFailed", failed.Status);
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.SimulateAsync(failed.Id, new(), default));
        f.Storage.Fail = false;
        var doc = await f.Service.UploadAsync(Upload(), default);
        await f.Service.SimulateAsync(doc.Id, new(), default);
        f.Storage.Objects[f.Storage.Objects.Keys.Single()] = [1, 2, 3];
        await Assert.ThrowsAsync<ConflictException>(() => f.Service.DownloadAsync(doc.Id, default));
    }

    [Fact]
    public async Task TC_COMPLIANCE_DEMO_006_Delete_failure_blocks_download_and_can_retry()
    {
        using var f = new Fixture();
        var doc = await f.Service.UploadAsync(Upload(), default);
        await f.Service.SimulateAsync(doc.Id, new(), default);
        f.Storage.Fail = true;
        await Assert.ThrowsAsync<ServiceUnavailableAppException>(() => f.Service.DeleteAsync(doc.Id, default));
        await Assert.ThrowsAsync<ForbiddenAppException>(() => f.Service.DownloadAsync(doc.Id, default));
        f.Storage.Fail = false;
        await f.Service.DeleteAsync(doc.Id, default);
        Assert.Empty(f.Storage.Objects);
    }

    [Fact]
    public async Task TC_COMPLIANCE_DEMO_007_Disabled_and_non_development_are_blocked()
    {
        using var f = new Fixture();
        f.Options.Enabled = false;
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.ListAsync(default));
        f.Options.Enabled = true;
        f.Environment.EnvironmentName = "Production";
        await Assert.ThrowsAsync<NotFoundException>(() => f.Service.ListAsync(default));
        using var services = new ServiceCollection().AddComplianceModule(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ComplianceDemo:Enabled"] = "true", ["ComplianceDemo:AccessKey"] = "test",
                ["ComplianceDemo:SecretKey"] = "test-only"
            }).Build(), f.Environment).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<ComplianceDemoOptions>>().Value);
    }

    [Fact]
    public async Task TC_COMPLIANCE_DEMO_008_Disabled_routes_are_absent_from_host()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var response = await client.GetAsync("/compliance/demo/documents");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void TC_COMPLIANCE_DEMO_009_Migration_matches_model_and_separate_schema()
    {
        using var db = new ComplianceDbContext(new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseSqlServer("Server=unused;Database=ScriptOnly;Integrated Security=true").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE TABLE [compliance].[DemoDocuments]", script);
        Assert.DoesNotContain("DROP TABLE", script);
    }

    [Fact]
    public async Task TC_COMPLIANCE_DEMO_011_Stale_simulation_cannot_overwrite_a_committed_result()
    {
        using var f = new Fixture();
        var doc = await f.Service.UploadAsync(Upload(), default);
        using var staleDb = new ComplianceDbContext(new DbContextOptionsBuilder<ComplianceDbContext>()
            .UseSqlite(f.Db.Database.GetDbConnection()).Options);
        await staleDb.DemoDocuments.SingleAsync(x => x.Id == doc.Id);
        await f.Service.SimulateAsync(doc.Id, new() { Scenario = "Infected" }, default);
        var staleService = new DemoDocumentService(staleDb, f.Storage, f.Actor, f.Accounts,
            Microsoft.Extensions.Options.Options.Create(f.Options), f.Environment);
        await Assert.ThrowsAsync<ConflictException>(() => staleService.SimulateAsync(doc.Id, new(), default));
        Assert.Equal("DemoBlocked", (await f.Service.GetAsync(doc.Id, default)).Status);
    }

    private static UploadDemoDocumentRequest Upload(string content = "%PDF-1.4 synthetic test") => new()
    {
        SyntheticDataConfirmed = true,
        File = new FormFile(new MemoryStream(Encoding.UTF8.GetBytes(content)), 0,
            Encoding.UTF8.GetByteCount(content), "File", "synthetic.pdf")
    };

    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public ComplianceDbContext Db { get; }
        public FakeStorage Storage { get; } = new();
        public Actor Actor { get; } = new();
        public Accounts Accounts { get; } = new();
        public ComplianceDemoOptions Options { get; } = new() { Enabled = true };
        public DemoEnvironment Environment { get; } = new();
        public DemoDocumentService Service { get; }
        public Fixture()
        {
            connection.Open();
            Db = new ComplianceDbContext(new DbContextOptionsBuilder<ComplianceDbContext>().UseSqlite(connection).Options);
            Db.Database.EnsureCreated();
            Service = new(Db, Storage, Actor, Accounts, Microsoft.Extensions.Options.Options.Create(Options), Environment);
        }
        public void Dispose() { Db.Dispose(); connection.Dispose(); }
    }

    internal sealed class FakeStorage : IPrivateDocumentStorage
    {
        public Dictionary<string, byte[]> Objects { get; } = [];
        public bool Fail { get; set; }
        private void Check() { if (Fail) throw new ServiceUnavailableAppException("Test storage unavailable"); }
        public Task PutAsync(string bucket, string key, byte[] content, string contentType, CancellationToken ct)
        { Check(); Objects[$"{bucket}/{key}"] = content; return Task.CompletedTask; }
        public Task<byte[]> ReadAsync(string bucket, string key, int maxBytes, CancellationToken ct)
        { Check(); return Task.FromResult(Objects[$"{bucket}/{key}"]); }
        public Task DeleteAsync(string bucket, string key, CancellationToken ct)
        { Check(); Objects.Remove($"{bucket}/{key}"); return Task.CompletedTask; }
    }
    internal sealed class Actor : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string? Email => "demo@example.com";
        public IReadOnlyList<string> Roles => [];
        public IReadOnlyList<string> Permissions => [];
        public bool HasPermission(string code) => false;
    }
    internal sealed class Accounts : IAccountDirectory
    {
        public bool Active { get; set; } = true;
        public Task<AccountSummary?> FindActiveAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AccountSummary?>(Active ? new(accountId, "demo@example.com", AccountGroups.Operator, true) : null);
    }
    internal sealed class DemoEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "ComplianceTests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
