using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransConnect.Common.Auth;
using TransConnect.Common.ExceptionHandling;
using TransConnect.Common.Response;
using TransConnect.Common.Transport;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Compliance.Controllers;
using TransConnect.Modules.Compliance.Options;
using TransConnect.Modules.Compliance.Persistence;
using TransConnect.Modules.Compliance.Services;
using TransConnect.Modules.Compliance.Storage;
using Xunit;

namespace TransConnect.Tests;

public sealed class ComplianceDemoHttpTests
{
    [Fact]
    public async Task TC_COMPLIANCE_DEMO_010_Multipart_contract_auth_ownership_and_binary_download()
        => await ExerciseContractAsync(false);

    [Fact]
    public async Task TC_COMPLIANCE_DEMO_011_Profile_multipart_versions_auth_and_legacy_route_isolation()
        => await ExerciseContractAsync(true);

    private static async Task ExerciseContractAsync(bool profile)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var host = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer().UseEnvironment("Development")
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddHttpContextAccessor();
                services.AddScoped<ICurrentUser, CurrentUser>();
                services.AddSingleton<IAccountDirectory, ComplianceDemoTests.Accounts>();
                services.AddSingleton<IPrivateDocumentStorage, ComplianceDemoTests.FakeStorage>();
                services.AddSingleton<IHostEnvironment>(new ComplianceDemoTests.DemoEnvironment());
                services.Configure<ComplianceDemoOptions>(o => o.Enabled = true);
                services.AddDbContext<ComplianceDbContext>(o => o.UseSqlite(connection));
                services.AddScoped<DemoDocumentService>();
                services.AddScoped<IDemoDocumentService>(sp => sp.GetRequiredService<DemoDocumentService>());
                services.AddScoped<IProfileDocumentService, ProfileDocumentService>();
                services.AddScoped<IProfileDocumentAccess, DriverAccess>();
                services.AddExceptionHandler<AppExceptionHandler>();
                services.AddProblemDetails();
                services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("test", _ => { });
                services.AddAuthorization();
                services.AddControllers().AddVietnameseApiValidation().AddApplicationPart(typeof(DemoDocumentsController).Assembly);
            }).Configure(app =>
            {
                app.UseExceptionHandler();
                app.UseApiStatusCodePages();
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(e => e.MapControllers());
            })).StartAsync();
        var server = host.GetTestServer();
        using (var scope = server.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ComplianceDbContext>().Database.EnsureCreatedAsync();
        using var client = server.CreateClient();
        var owner = Guid.NewGuid().ToString();
        var route = profile ? $"/compliance/demo/profile-documents/Driver/{owner}/DriverLicense" : "/compliance/demo/documents";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route)).StatusCode);
        client.DefaultRequestHeaders.Add("X-Test-Account", owner);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.4 synthetic")), "File", "sample.pdf");
        form.Add(new StringContent("true"), "SyntheticDataConfirmed");
        var upload = await client.PostAsync(route, form);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var json = JsonDocument.Parse(await upload.Content.ReadAsStringAsync()).RootElement.GetProperty("data");
        if (profile)
        {
            Assert.Equal(1, json.GetProperty("versionNumber").GetInt32());
            json = json.GetProperty("file");
        }
        var id = json.GetProperty("id").GetGuid();
        using (var scope = server.Services.CreateScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<ComplianceDbContext>()
                .DemoDocuments.AsNoTracking().SingleAsync(x => x.Id == id);
            var prefix = profile ? $"demo/profiles/Driver/{Guid.Parse(owner):N}/DriverLicense/personal"
                : $"demo/accounts/{Guid.Parse(owner):N}";
            Assert.Equal($"{prefix}/{id:N}/original", saved.ObjectKey);
        }
        var versionRoute = profile ? $"/compliance/demo/profile-documents/versions/{id}" : $"{route}/{id}";
        if (profile)
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/compliance/demo/documents/{id}/download")).StatusCode);
        Assert.Equal("SIMULATED", json.GetProperty("processingMode").GetString());
        Assert.False(json.TryGetProperty("objectKey", out _));
        Assert.False(json.TryGetProperty("bucket", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"{versionRoute}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"{versionRoute}/simulate", new { scenario = "RealScan" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"{versionRoute}/simulate", new { scenario = "Success" })).StatusCode);
        var download = await client.GetAsync($"{versionRoute}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/octet-stream", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.True(download.Headers.CacheControl!.NoStore);
        Assert.Equal("DEMO-UNSCANNED", Assert.Single(download.Headers.GetValues("X-Document-Mode")));
        client.DefaultRequestHeaders.Remove("X-Test-Account");
        client.DefaultRequestHeaders.Add("X-Test-Account", Guid.NewGuid().ToString());
        Assert.Equal(profile ? HttpStatusCode.Forbidden : HttpStatusCode.NotFound, (await client.GetAsync($"{versionRoute}/download")).StatusCode);
    }

    // HTTP fixture validates routing/binding; real Operator ACL is covered in ProfileDocumentTests.
    private sealed class DriverAccess(ICurrentUser actor) : IProfileDocumentAccess
    {
        public Task<ProfileDocumentScope> AuthorizeAsync(string subjectType, Guid subjectId, bool write, CancellationToken ct)
        {
            if (subjectType != "Driver" || subjectId != actor.UserId) throw new ForbiddenAppException();
            return Task.FromResult(new ProfileDocumentScope(actor.UserId!.Value, null));
        }
    }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-Account"], out var id))
                return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
