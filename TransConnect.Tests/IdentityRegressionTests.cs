using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Options;
using TransConnect.Modules.Identity.Persistence;
using TransConnect.Modules.Identity.Services;
using Xunit;

namespace TransConnect.Tests;

public class IdentityRegressionTests
{
    [Theory]
    [InlineData("consume")]
    [InlineData("attempt")]
    [InlineData("issue")]
    public async Task TC_OTP_004_Concurrent_operations_preserve_limits(string operation)
    {
        var path = Path.Combine(Path.GetTempPath(), $"transconnect-otp-{Guid.NewGuid():N}.db");
        try
        {
            var gate = new UnprotectedReadGate(operation == "issue" ? "COUNT(" : "LIMIT 1");
            var options = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseSqlite($"Data Source={path};Pooling=False").AddInterceptors(gate).Options;
            var limits = Options.Create(new VerificationOptions { MaxRequestsPerHour = 2, MaxAttempts = 2 });
            var hasher = new PasswordHasher();
            var user = new User { Email = "concurrent@example.com", FullName = "Concurrent" };
            string code;
            await using (var db = new IdentityDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                db.Users.Add(user);
                await db.SaveChangesAsync();
                code = await new VerificationCodeService(db, hasher, limits)
                    .IssueAsync(user.Id, VerificationPurpose.PasswordReset);
            }
            gate.Enabled = true;
            async Task<bool> Execute()
            {
                await using var db = new IdentityDbContext(options);
                var service = new VerificationCodeService(db, hasher, limits);
                try
                {
                    if (operation == "issue") await service.IssueAsync(user.Id, VerificationPurpose.PasswordReset);
                    else await service.ConsumeAsync(user.Id, VerificationPurpose.PasswordReset,
                        operation == "attempt" ? "invalid" : code);
                    return true;
                }
                catch (UnauthorizedAppException) { return false; }
                catch (TooManyRequestsException) { return false; }
            }
            var results = await Task.WhenAll(Task.Run(Execute), Task.Run(Execute)).WaitAsync(TimeSpan.FromSeconds(30));
            gate.Enabled = false;
            await using var verification = new IdentityDbContext(options);
            if (operation == "attempt")
            {
                Assert.All(results, success => Assert.False(success));
                var saved = await verification.VerificationCodes.SingleAsync();
                Assert.Equal(2, saved.AttemptCount);
                Assert.True(saved.IsUsed);
            }
            else
            {
                Assert.Single(results.Where(success => success));
                Assert.Equal(operation == "issue" ? 2 : 1, await verification.VerificationCodes.CountAsync());
                Assert.Equal(operation == "issue" ? 1 : 0, await verification.VerificationCodes.CountAsync(v => !v.IsUsed));
            }
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }

    [Fact]
    public async Task TC_IAM_008_Reset_failure_rolls_back_password_otp_and_session_changes()
    {
        var fault = new RevokeFailure();
        using var factory = new ApiFactory(interceptor: fault);
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = await AuthLifecycleTests.SeedUser(scope.ServiceProvider);
        var originalHash = user.PasswordHash;
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        await auth.LoginAsync(new LoginRequest { Email = user.Email, Password = "password123" });
        var code = await scope.ServiceProvider.GetRequiredService<IVerificationCodeService>()
            .IssueAsync(user.Id, VerificationPurpose.PasswordReset);
        fault.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => auth.ResetPasswordAsync(
            new ResetPasswordRequest { Email = user.Email, Code = code, NewPassword = "new123" }));
        fault.Enabled = false;
        db.ChangeTracker.Clear();
        var saved = await db.Users.SingleAsync();
        Assert.Equal(originalHash, saved.PasswordHash);
        Assert.Null(saved.EmailVerifiedAt);
        Assert.False((await db.VerificationCodes.SingleAsync()).IsUsed);
        Assert.False((await db.RefreshTokens.SingleAsync()).IsRevoked);
        await auth.ResetPasswordAsync(new ResetPasswordRequest { Email = user.Email, Code = code, NewPassword = "new123" });
        Assert.True((await db.RefreshTokens.AsNoTracking().SingleAsync()).IsRevoked);
    }

    [Fact]
    public async Task TC_IAM_009_Invalid_reset_keeps_failed_attempt_count()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = await AuthLifecycleTests.SeedUser(scope.ServiceProvider);
        await scope.ServiceProvider.GetRequiredService<IVerificationCodeService>().IssueAsync(user.Id, VerificationPurpose.PasswordReset);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => scope.ServiceProvider.GetRequiredService<IAuthService>()
            .ResetPasswordAsync(new ResetPasswordRequest { Email = user.Email, Code = "invalid", NewPassword = "new123" }));
        Assert.Equal(1, (await db.VerificationCodes.AsNoTracking().SingleAsync()).AttemptCount);
    }

    [Fact]
    public async Task TC_RBAC_001_Deleted_names_can_be_recreated_without_restoring_old_grants()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = await AuthLifecycleTests.SeedUser(scope.ServiceProvider);
        var roles = scope.ServiceProvider.GetRequiredService<IRoleService>();
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionService>();
        var role = await roles.CreateAsync(new CreateRoleRequest { Name = "Reusable" });
        var permission = await permissions.CreateAsync(new CreatePermissionRequest { Code = "reusable.read" });
        await roles.AssignToUserAsync(role.Id, user.Id);
        await roles.AssignPermissionAsync(role.Id, permission.Id);
        await permissions.DeleteAsync(permission.Id);
        var replacementPermission = await permissions.CreateAsync(new CreatePermissionRequest { Code = "reusable.read" });
        Assert.NotEqual(permission.Id, replacementPermission.Id);
        Assert.Empty(await permissions.GetPermissionCodesAsync(user.Id));
        await roles.DeleteAsync(role.Id);
        var replacementRole = await roles.CreateAsync(new CreateRoleRequest { Name = "Reusable" });
        Assert.NotEqual(role.Id, replacementRole.Id);
        Assert.Empty(await permissions.GetRoleNamesAsync(user.Id));
        await Assert.ThrowsAsync<ConflictException>(() => roles.CreateAsync(new CreateRoleRequest { Name = "Reusable" }));
        await Assert.ThrowsAsync<ConflictException>(() => permissions.CreateAsync(new CreatePermissionRequest { Code = "reusable.read" }));
    }

    [Fact]
    public async Task TC_IAM_010_Register_and_reset_accept_six_but_reject_five_characters()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        await scope.ServiceProvider.GetRequiredService<TransConnect.Modules.Notification.Persistence.NotificationDbContext>().Database.EnsureCreatedAsync();
        const string email = "six@example.com";
        using var shortRegister = await client.PostAsJsonAsync("/auth/register", new { email, password = "12345", fullName = "Six" });
        Assert.Equal(HttpStatusCode.BadRequest, shortRegister.StatusCode);
        using var register = await client.PostAsJsonAsync("/auth/register", new { email, password = "123456", fullName = "Six" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var user = await db.Users.SingleAsync();
        var code = await scope.ServiceProvider.GetRequiredService<IVerificationCodeService>().IssueAsync(user.Id, VerificationPurpose.PasswordReset);
        using var shortReset = await client.PostAsJsonAsync("/auth/reset-password", new { email, code, newPassword = "12345" });
        Assert.Equal(HttpStatusCode.BadRequest, shortReset.StatusCode);
        using var reset = await client.PostAsJsonAsync("/auth/reset-password", new { email, code, newPassword = "654321" });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        using var login = await client.PostAsJsonAsync("/auth/login", new { email, password = "654321" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Theory]
    [InlineData("roles")]
    [InlineData("permissions")]
    [InlineData("rename")]
    public async Task TC_RBAC_002_Concurrent_duplicate_writes_return_409(string operation)
    {
        var path = Path.Combine(Path.GetTempPath(), $"transconnect-rbac-{Guid.NewGuid():N}.db");
        try
        {
            var gate = new UniqueReadGate(operation == "permissions" ? "Permissions" : "Roles");
            using var factory = new ApiFactory(path, gate);
            using var client = factory.CreateApiClient();
            Guid firstId;
            Guid secondId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                await db.Database.EnsureCreatedAsync();
                var first = new Role { Name = "First" };
                var second = new Role { Name = "Second" };
                db.AddRange(first, second);
                await db.SaveChangesAsync();
                firstId = first.Id;
                secondId = second.Id;
                var token = scope.ServiceProvider.GetRequiredService<ITokenService>()
                    .GenerateAccessToken(new User(), ["Admin"], ["roles.manage", "permissions.manage"]);
                client.DefaultRequestHeaders.Authorization = new("Bearer", token.AccessToken);
            }
            gate.Enabled = true;
            Task<HttpResponseMessage> Send(Guid id) => operation switch
            {
                "rename" => client.PutAsJsonAsync($"/roles/{id}", new { name = "Duplicate" }),
                "permissions" => client.PostAsJsonAsync("/permissions", new { code = "duplicate.read" }),
                _ => client.PostAsJsonAsync("/roles", new { name = "Duplicate" })
            };
            var responses = await Task.WhenAll(Task.Run(() => Send(firstId)), Task.Run(() => Send(secondId)))
                .WaitAsync(TimeSpan.FromSeconds(30));
            try
            {
                Assert.Single(responses.Where(r => r.StatusCode == HttpStatusCode.OK));
                var conflict = Assert.Single(responses.Where(r => r.StatusCode == HttpStatusCode.Conflict));
                var body = await conflict.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                Assert.Equal(AppErrorCodes.Conflict, body.GetProperty("errorCode").GetString());
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
        finally { foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix); }
    }

    [Fact]
    public async Task TC_RBAC_003_Seeder_restores_default_admin_role_after_soft_delete()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await IdentitySeeder.SeedAsync(db, hasher);
        var admin = await db.Users.SingleAsync();
        var originalHash = admin.PasswordHash;
        var roles = scope.ServiceProvider.GetRequiredService<IRoleService>();
        var permissions = scope.ServiceProvider.GetRequiredService<IPermissionService>();
        foreach (var role in await db.Roles.ToListAsync()) await roles.DeleteAsync(role.Id);
        foreach (var permission in await db.Permissions.ToListAsync()) await permissions.DeleteAsync(permission.Id);
        db.ChangeTracker.Clear();
        await IdentitySeeder.SeedAsync(db, hasher);
        await IdentitySeeder.SeedAsync(db, hasher);
        Assert.Contains("Admin", await permissions.GetRoleNamesAsync(admin.Id));
        Assert.Contains("roles.manage", await permissions.GetPermissionCodesAsync(admin.Id));
        Assert.Equal(2, await db.Roles.CountAsync());
        Assert.Equal(4, await db.Permissions.CountAsync());
        Assert.Contains("organizations.manage", await permissions.GetPermissionCodesAsync(admin.Id));
        Assert.Equal(originalHash, (await db.Users.SingleAsync()).PasswordHash);
    }

    private sealed class UniqueReadGate(string table) : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("SELECT EXISTS") && command.CommandText.Contains($"FROM \"{table}\""))
            {
                if (Interlocked.Increment(ref _arrivals) >= 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }

    // Synchronize the vulnerable reads only. Protected operations must acquire their
    // database transaction/lock BEFORE reading, so they cannot meet at this barrier.
    private sealed class UnprotectedReadGate(string marker) : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.Transaction is null && command.CommandText.Contains("FROM \"VerificationCodes\"")
                && command.CommandText.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                if (Interlocked.Increment(ref _arrivals) >= 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }

    private sealed class RevokeFailure : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.StartsWith("UPDATE \"RefreshTokens\""))
                throw new InvalidOperationException("Injected refresh revocation failure");
            return ValueTask.FromResult(result);
        }
    }
}
