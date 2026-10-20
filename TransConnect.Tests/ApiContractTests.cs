using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Persistence;
using TransConnect.Modules.Identity.Services;
using TransConnect.Modules.Notification.Workers;
using TransConnect.Modules.Notification.Persistence;
using TransConnect.Modules.Operator.Persistence;
using Xunit;

namespace TransConnect.Tests;

public class ApiContractTests
{
    [Theory]
    [InlineData("/auth/login", "{}", "email", "Vui lòng nhập Email.")]
    [InlineData("/auth/login", "{\"email\":\"bad\",\"password\":\"123456\"}", "email", "Email không đúng định dạng.")]
    [InlineData("/auth/register", "{\"email\":\"a@b.com\",\"password\":\"x\",\"fullName\":\"Tên\"}", "password", "Mật khẩu phải có ít nhất 6 ký tự.")]
    public async Task TC_API_001_Field_errors_are_Vietnamese(string path, string body, string field, string message)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var response = await client.PostAsync(path, Json(body));
        var json = await AssertFailure(response, HttpStatusCode.BadRequest, AppErrorCodes.ValidationError);
        Assert.Contains(message, json.GetProperty("errors").GetProperty(field).EnumerateArray().Select(x => x.GetString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{\"email\":123,\"password\":\"abc\"}")]
    public async Task TC_API_002_Binding_errors_use_envelope_without_parser_details(string body)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var response = await client.PostAsync("/auth/login", Json(body));
        var json = await AssertFailure(response, HttpStatusCode.BadRequest, AppErrorCodes.ValidationError);
        Assert.NotEmpty(json.GetProperty("errors").EnumerateObject());
        Assert.DoesNotContain("System.", json.GetRawText());
        Assert.DoesNotContain("BytePosition", json.GetRawText());
        Assert.DoesNotContain("field is required", json.GetRawText());
    }

    [Theory]
    [InlineData("/api/auth/me", 404, AppErrorCodes.NotFound)]
    [InlineData("/api/v1/auth/me", 404, AppErrorCodes.NotFound)]
    [InlineData("/auth/refresh-token", 404, AppErrorCodes.NotFound)]
    [InlineData("/auth/login", 405, AppErrorCodes.MethodNotAllowed)]
    [InlineData("/auth/me", 401, AppErrorCodes.Unauthorized)]
    [InlineData("/roles", 401, AppErrorCodes.Unauthorized)]
    [InlineData("/permissions", 401, AppErrorCodes.Unauthorized)]
    public async Task TC_API_003_Routes_and_empty_errors_use_envelope(string path, int status, string code)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var response = await client.GetAsync(path);
        await AssertFailure(response, (HttpStatusCode)status, code);
    }

    [Fact]
    public async Task TC_API_004_Unsupported_content_type_uses_envelope()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var response = await client.PostAsync("/auth/login", new StringContent("hello"));
        await AssertFailure(response, HttpStatusCode.UnsupportedMediaType, AppErrorCodes.UnsupportedMediaType);
    }

    [Theory]
    [InlineData("validation", 400, AppErrorCodes.ValidationError)]
    [InlineData("conflict", 409, AppErrorCodes.Conflict)]
    [InlineData("missing", 404, AppErrorCodes.NotFound)]
    [InlineData("unauthorized", 401, AppErrorCodes.Unauthorized)]
    [InlineData("forbidden", 403, AppErrorCodes.Forbidden)]
    [InlineData("limited", 429, AppErrorCodes.TooManyRequests)]
    [InlineData("unexpected", 500, AppErrorCodes.InternalError)]
    public async Task TC_API_005_Exception_handler_preserves_details_and_hides_internal_errors(string kind, int status, string code)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var response = await client.GetAsync($"/_contract-tests/{kind}");
        var json = await AssertFailure(response, (HttpStatusCode)status, code);
        Assert.DoesNotContain("sensitive-internal-detail", json.GetRawText());
        if (kind == "validation")
            Assert.Equal("Email không hợp lệ.", json.GetProperty("errors").GetProperty("email")[0].GetString());
    }

    [Fact]
    public async Task TC_IAM_001_Me_reads_current_database_profile_and_permissions_without_secrets()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        var userId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = new User { Id = userId, Email = "me@example.com", FullName = "Tên trong DB", PasswordHash = "secret-hash", GoogleId = "secret-google", EmailVerifiedAt = DateTime.UtcNow };
            var role = new Role { Name = "User" };
            var permission = new Permission { Code = "profile.read" };
            db.AddRange(user, role, permission);
            db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            await db.SaveChangesAsync();
            // JWT cố ý chứa hồ sơ và quyền cũ để xác minh /me thực sự đọc DB.
            user.FullName = "Tên cũ trong JWT";
            client.DefaultRequestHeaders.Authorization = Bearer(scope.ServiceProvider, user, ["old.permission"]);
        }

        using var response = await client.GetAsync("/auth/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var data = json.GetProperty("data");
        Assert.Equal(userId, data.GetProperty("userId").GetGuid());
        Assert.Equal("Tên trong DB", data.GetProperty("fullName").GetString());
        Assert.True(data.GetProperty("emailVerified").GetBoolean());
        Assert.Contains("profile.read", data.GetProperty("permissions").EnumerateArray().Select(x => x.GetString()));
        Assert.False(data.TryGetProperty("passwordHash", out _));
        Assert.False(data.TryGetProperty("googleId", out _));
        Assert.False(data.TryGetProperty("refreshToken", out _));
        Assert.False(data.TryGetProperty("accessToken", out _));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("deleted")]
    [InlineData("inactive")]
    public async Task TC_IAM_002_Me_rejects_unavailable_accounts(string state)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = new User { Email = "me@example.com", FullName = "Tên", IsActive = state != "inactive", DeletedAt = state == "deleted" ? DateTime.UtcNow : null };
            if (state != "missing") { db.Users.Add(user); await db.SaveChangesAsync(); }
            client.DefaultRequestHeaders.Authorization = Bearer(scope.ServiceProvider, user, []);
        }
        using var response = await client.GetAsync("/auth/me");
        await AssertFailure(response, HttpStatusCode.Unauthorized, AppErrorCodes.Unauthorized);
    }

    [Fact]
    public async Task TC_API_006_Permission_filter_returns_Vietnamese_forbidden()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = new User { Email = "no-permission@example.com", FullName = "No permission" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Authorization = Bearer(scope.ServiceProvider, user, []);
        using var response = await client.GetAsync("/roles");
        var json = await AssertFailure(response, HttpStatusCode.Forbidden, AppErrorCodes.Forbidden);
        Assert.Equal("Bạn không có quyền thực hiện thao tác này.", json.GetProperty("message").GetString());
    }

    private static AuthenticationHeaderValue Bearer(IServiceProvider services, User user, string[] permissions) =>
        new("Bearer", services.GetRequiredService<ITokenService>().GenerateAccessToken(user, ["User"], permissions).AccessToken);

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    private static async Task<JsonElement> AssertFailure(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.False(json.GetProperty("succeeded").GetBoolean());
        Assert.Equal(code, json.GetProperty("errorCode").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("data").ValueKind);
        Assert.True(json.TryGetProperty("errors", out _));
        return json;
    }
}

internal sealed class ApiFactory(string? databasePath = null, Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor? interceptor = null,
    TransConnect.Common.Transport.ITransportObligations? obligations = null, string? operatorDatabasePath = null) : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly SqliteConnection _notificationConnection = new("Data Source=:memory:");
    private readonly SqliteConnection _operatorConnection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        _notificationConnection.Open();
        _operatorConnection.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:Key", "test-only-key-0123456789-abcdefghijklmnopqrstuvwxyz");
        builder.UseSetting("Jwt:Issuer", "TransConnect.Tests");
        builder.UseSetting("Jwt:Audience", "TransConnect.Tests");
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            // Không migrate SQL Server, chạy SMTP hay worker trong kiểm thử API.
            foreach (var registration in services.Where(d =>
                         d.ImplementationType == typeof(EmailDispatchWorker)
                         || d.ImplementationType == typeof(TransConnect.Common.Maintenance.MaintenanceWorker)
                         || d.ServiceType == typeof(IdentityDbContext)
                         || d.ServiceType == typeof(NotificationDbContext)
                         || d.ServiceType == typeof(OperatorDbContext)
                         || (d.ServiceType.IsGenericType && (d.ServiceType.GenericTypeArguments.Contains(typeof(IdentityDbContext))
                             || d.ServiceType.GenericTypeArguments.Contains(typeof(NotificationDbContext))
                             || d.ServiceType.GenericTypeArguments.Contains(typeof(OperatorDbContext))))).ToArray())
                services.Remove(registration);
            services.AddDbContext<IdentityDbContext>(options =>
            {
                if (databasePath is null) options.UseSqlite(_connection);
                else options.UseSqlite($"Data Source={databasePath};Pooling=False");
                if (interceptor is not null) options.AddInterceptors(interceptor);
            });
            services.AddDbContext<NotificationDbContext>(options => options.UseSqlite(_notificationConnection));
            services.AddDbContext<OperatorDbContext>(options =>
            {
                if (operatorDatabasePath is null) options.UseSqlite(_operatorConnection);
                else options.UseSqlite($"Data Source={operatorDatabasePath};Pooling=False");
            });
            if (obligations is not null)
                services.AddSingleton<TransConnect.Common.Transport.ITransportObligations>(obligations);
            services.AddControllers().AddApplicationPart(typeof(ContractErrorsController).Assembly);
        });
    }

    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
    });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            _notificationConnection.Dispose();
            _operatorConnection.Dispose();
        }
    }
}

// Chỉ đăng ký trong test host; không xuất hiện trong ứng dụng thật.
[ApiController]
[Route("_contract-tests")]
public class ContractErrorsController : ControllerBase
{
    [HttpGet("{kind}")]
    public IActionResult Get(string kind) => throw (kind switch
    {
        "validation" => new ValidationAppException(new Dictionary<string, string[]> { ["email"] = ["Email không hợp lệ."] }),
        "conflict" => new ConflictException("Dữ liệu đã tồn tại."),
        "missing" => new NotFoundException("Không tìm thấy dữ liệu."),
        "unauthorized" => new UnauthorizedAppException("Vui lòng đăng nhập lại."),
        "forbidden" => new ForbiddenAppException(),
        "limited" => new TooManyRequestsException("Vui lòng thử lại sau."),
        _ => new Exception("sensitive-internal-detail")
    });
}
