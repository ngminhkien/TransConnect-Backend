using System.Net;
using System.Data.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Options;
using TransConnect.Modules.Identity.Persistence;
using TransConnect.Modules.Identity.Services;
using Xunit;

namespace TransConnect.Tests;

public sealed class FoundationClosureSecurityTests
{
    [Fact]
    public async Task TC_CLOSURE_011_Login_rechecks_password_changed_before_token_transaction()
    {
        var path = Path.Combine(Path.GetTempPath(), "closure-login-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var boundary = new TransactionBoundary();
            using var factory = new ApiFactory(path, boundary);
            using var client = factory.CreateApiClient();
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = await AuthLifecycleTests.SeedUser(scope.ServiceProvider);
            var hash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash("changed-before-issue");
            boundary.BeforeNextTransaction = async () => {
                await using var concurrent = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
                    .UseSqlite($"Data Source={path};Pooling=False").Options);
                await concurrent.Users.Where(x => x.Id == user.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.PasswordHash, hash));
            };
            using var old = await client.PostAsJsonAsync("/auth/login", new { email = user.Email, password = "password123" });
            Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
            Assert.Equal(0, await db.RefreshTokens.CountAsync());
            using var current = await client.PostAsJsonAsync("/auth/login", new { email = user.Email, password = "changed-before-issue" });
            Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }

    private sealed class TransactionBoundary : DbTransactionInterceptor
    {
        public Func<Task>? BeforeNextTransaction { get; set; }
        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection, TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            var change = BeforeNextTransaction;
            BeforeNextTransaction = null;
            if (change is not null) await change();
            return result;
        }
    }

    [Fact]
    public async Task TC_CLOSURE_004_Profile_whitelist_and_password_change_revoke_refresh_atomically()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = await AuthLifecycleTests.SeedUser(scope.ServiceProvider);
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var login = await auth.LoginAsync(new() { Email = user.Email, Password = "password123" });
        client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        using var profile = await client.PutAsJsonAsync("/auth/me", new {
            fullName = "New name", phoneNumber = "0901234567", email = "attacker@example.com", isActive = false,
            accountGroup = "InternalAdmin", avatarUrl = "https://attacker.example/image"
        });
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var saved = await db.Users.AsNoTracking().SingleAsync();
        Assert.Equal(user.Email, saved.Email);
        Assert.Equal("Customer", saved.AccountGroup);
        Assert.True(saved.IsActive);
        Assert.Null(saved.AvatarUrl);
        using var wrong = await client.PostAsJsonAsync("/auth/change-password", new { currentPassword = "wrong", newPassword = "new-password" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.False((await db.RefreshTokens.AsNoTracking().SingleAsync()).IsRevoked);
        using var changed = await client.PostAsJsonAsync("/auth/change-password", new { currentPassword = "password123", newPassword = "new-password" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var refresh = await db.RefreshTokens.AsNoTracking().SingleAsync();
        Assert.True(refresh.IsRevoked);
        Assert.Equal("PasswordChanged", refresh.RevokedReason);
        using var old = await client.PostAsJsonAsync("/auth/login", new { email = user.Email, password = "password123" });
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        using var next = await client.PostAsJsonAsync("/auth/login", new { email = user.Email, password = "new-password" });
        Assert.Equal(HttpStatusCode.OK, next.StatusCode);
    }

    [Fact]
    public async Task TC_CLOSURE_005_Google_only_cannot_set_first_password_through_change_password()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = new User { Email = "google@example.com", FullName = "Google", GoogleId = "sub" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateAccessToken(user, [], []).AccessToken);
        using var result = await client.PostAsJsonAsync("/auth/change-password", new { currentPassword = "anything", newPassword = "new-password" });
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Null((await db.Users.AsNoTracking().SingleAsync()).PasswordHash);
    }

    [Fact]
    public async Task TC_CLOSURE_006_Lower_admin_cannot_read_or_lock_internal_admin()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var lower = new User { AccountGroup = "InternalAdmin", Email = "lower@example.com", FullName = "Lower" };
        var higher = new User { AccountGroup = "InternalAdmin", Email = "higher@example.com", FullName = "Higher" };
        var customer = new User { Email = "customer@example.com", FullName = "Customer" };
        var role = new Role { Name = "AccountManager" };
        var permission = new Permission { Code = "users.manage" };
        db.AddRange(lower, higher, customer, role, permission);
        db.UserRoles.Add(new() { UserId = lower.Id, RoleId = role.Id });
        db.RolePermissions.Add(new() { RoleId = role.Id, PermissionId = permission.Id });
        await db.SaveChangesAsync();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateAccessToken(lower, [], ["admins.manage", "users.manage"]).AccessToken);
        using var read = await client.GetAsync("/accounts/" + higher.Id);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        using var locked = await client.PutAsJsonAsync("/accounts/" + higher.Id + "/status", new { isActive = false, reason = "No" });
        Assert.Equal(HttpStatusCode.Forbidden, locked.StatusCode);
        using var allowed = await client.PutAsJsonAsync("/accounts/" + customer.Id + "/status", new { isActive = false, reason = "Allowed" });
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.True((await db.Users.AsNoTracking().SingleAsync(x => x.Id == higher.Id)).IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TC_CLOSURE_007_Avatar_saves_only_provider_url_and_hides_provider_errors(bool dynamicFolders)
    {
        using var factory = new ApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = await AuthLifecycleTests.SeedUser(scope.ServiceProvider);
        var actor = new Actor(user.Id);
        var handler = new AvatarHandler { DynamicFolders = dynamicFolders, AccountId = user.Id };
        using var http = new HttpClient(handler);
        var service = new AvatarService(db, actor, http, Options.Create(new CloudinaryOptions {
            CloudName = "test-cloud", ApiKey = "test-key", ApiSecret = "secret-never-return",
            UseDynamicFolders = dynamicFolders
        }));
        var png = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3, 4 };
        IFormFile File(byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "untrusted.svg");
        await Assert.ThrowsAsync<ValidationAppException>(() => service.UploadAsync(File(Encoding.UTF8.GetBytes("<svg>bad</svg>")), default));
        Assert.Equal(0, handler.Calls);
        var url = await service.UploadAsync(File(png), default);
        Assert.Equal("https://res.cloudinary.com/test-cloud/image/upload/avatar.jpg", url);
        Assert.Equal(url, (await db.Users.AsNoTracking().SingleAsync()).AvatarUrl);
        handler.Fail = true;
        var error = await Assert.ThrowsAsync<ServiceUnavailableAppException>(() => service.UploadAsync(File(png), default));
        Assert.DoesNotContain("secret-never-return", error.Message);
        Assert.Equal(url, (await db.Users.AsNoTracking().SingleAsync()).AvatarUrl);
    }

    private sealed class AvatarHandler : HttpMessageHandler
    {
        public bool DynamicFolders { get; init; }
        public Guid AccountId { get; init; }
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Equal("api.cloudinary.com", request.RequestUri!.Host);
            Assert.IsType<MultipartFormDataContent>(request.Content);
            var parts = (MultipartFormDataContent)request.Content!;
            var fields = new Dictionary<string, string>();
            foreach (var part in parts)
                fields[part.Headers.ContentDisposition!.Name!.Trim('"')] = await part.ReadAsStringAsync(ct);
            Assert.Equal($"transconnect/development/avatars/customer/{AccountId:N}", fields["public_id"]);
            Assert.Equal(DynamicFolders, fields.ContainsKey("asset_folder"));
            if (DynamicFolders) Assert.Equal("transconnect/development/avatars/customer", fields["asset_folder"]);
            var body = await request.Content!.ReadAsStringAsync(ct);
            Assert.Contains("signature", body);
            Assert.DoesNotContain("secret-never-return", body);
            return new(Fail ? HttpStatusCode.BadRequest : HttpStatusCode.OK) {
                Content = JsonContent.Create(Fail ? new { error = "secret-never-return" } : (object)new { secure_url = "https://res.cloudinary.com/test-cloud/image/upload/avatar.jpg" })
            };
        }
    }

    private sealed class Actor(Guid id) : ICurrentUser
    {
        public bool IsAuthenticated => true;
        public Guid? UserId => id;
        public string? Email => "lifecycle@example.com";
        public IReadOnlyList<string> Roles => [];
        public IReadOnlyList<string> Permissions => [];
        public bool HasPermission(string permissionCode) => false;
    }
}
