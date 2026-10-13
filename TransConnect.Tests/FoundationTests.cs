using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using TransConnect.Common.Auth;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Persistence;
using TransConnect.Modules.Identity.Services;
using TransConnect.Modules.Notification.Persistence;
using TransConnect.Modules.Operator.Persistence;
using Xunit;

namespace TransConnect.Tests;

public class FoundationTests
{
    [Fact]
    public async Task TC_FOUNDATION_001_Account_groups_isolate_login_otp_reset_and_profile()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var customer = await app.Post("/auth/register", new
        { email = "SAME@example.com", password = "123456", fullName = "Customer", accountGroup = "Customer" });
        var partner = await app.Post("/auth/register", new
        { email = "same@example.com", password = "654321", fullName = "Operator", accountGroup = "Operator" });
        Assert.NotEqual(customer.GetProperty("accountId").GetGuid(), partner.GetProperty("accountId").GetGuid());
        await app.Post("/auth/register", new
        { email = "same@example.com", password = "123456", fullName = "Duplicate", accountGroup = "Operator" }, HttpStatusCode.Conflict);
        await app.Post("/auth/register", new
        { email = "admin@example.com", password = "123456", fullName = "Admin", accountGroup = "InternalAdmin" }, HttpStatusCode.BadRequest);
        await app.Post("/auth/login", new { email = "same@example.com", password = "123456", accountGroup = "Operator" }, HttpStatusCode.Unauthorized);
        var login = await app.Post("/auth/login", new { email = "same@example.com", password = "654321", accountGroup = "Operator" });
        app.Client.DefaultRequestHeaders.Authorization = new("Bearer", login.GetProperty("accessToken").GetString());
        var me = await app.Get("/auth/me");
        Assert.Equal("Operator", me.GetProperty("accountGroup").GetString());
        await app.Post("/auth/forgot-password", new { email = "same@example.com", accountGroup = "Operator" });
        var otp = await app.LatestOtp("same@example.com");
        await app.Post("/auth/reset-password", new
        { email = "same@example.com", code = otp, newPassword = "abcdef", accountGroup = "Customer" }, HttpStatusCode.Unauthorized);
        await app.Post("/auth/reset-password", new
        { email = "same@example.com", code = otp, newPassword = "abcdef", accountGroup = "Operator" });
        await app.Post("/auth/login", new { email = "same@example.com", password = "123456", accountGroup = "Customer" });
        await app.Post("/auth/login", new { email = "same@example.com", password = "abcdef", accountGroup = "Operator" });
    }

    [Fact]
    public async Task TC_FOUNDATION_002_Approval_owner_invitation_quota_tenant_and_immediate_revocation()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var owner = await app.User("owner@example.com");
        var employee = await app.User("employee@example.com");
        var stranger = await app.User("stranger@example.com");
        var organization = await app.Organization("ORG-A", owner.Email);
        app.ActAs(owner);
        await app.Get($"/organizations/{organization}", HttpStatusCode.Forbidden);
        app.Admin();
        await app.Post($"/organizations/{organization}/review", new { approved = true, note = "Reviewed original registration." });
        var token = await app.InvitationToken(owner.Email);
        app.ActAs(stranger);
        await app.Post("/organizations/invitations/accept", new { token }, HttpStatusCode.Unauthorized);
        app.ActAs(owner);
        await app.Post("/organizations/invitations/accept", new { token });
        await app.Post("/organizations/invitations/accept", new { token }, HttpStatusCode.Unauthorized);
        await app.Post($"/organizations/{organization}/invitations", new { email = employee.Email, role = "Manager" });
        var employeeToken = await app.InvitationToken(employee.Email);
        await app.Post($"/organizations/{organization}/invitations", new { email = "pending@example.com", role = "Dispatcher" });
        await app.Post($"/organizations/{organization}/invitations", new { email = "overflow@example.com", role = "Dispatcher" }, HttpStatusCode.Conflict);
        app.ActAs(employee);
        var member = await app.Post("/organizations/invitations/accept", new { token = employeeToken });
        await app.Get($"/organizations/{organization}/vehicles");
        await app.Post($"/organizations/{organization}/invitations", new { email = stranger.Email, role = "Manager" }, HttpStatusCode.Forbidden);
        app.ActAs(stranger);
        await app.Get($"/organizations/{organization}/vehicles", HttpStatusCode.Forbidden);
        app.ActAs(owner);
        await app.Put($"/organizations/{organization}/members/{member.GetProperty("id").GetGuid()}",
            new { role = "Dispatcher", isActive = false });
        app.ActAs(employee); // Same signed JWT remains valid; membership is checked in DB.
        await app.Get($"/organizations/{organization}/vehicles", HttpStatusCode.Forbidden);
        app.ActAs(owner);
        var audit = await app.Get($"/organizations/{organization}/audit");
        Assert.Contains(audit.EnumerateArray(), x => x.GetProperty("action").GetString() == "member.update");
    }

    [Fact]
    public async Task TC_FOUNDATION_003_Driver_consent_multiple_organizations_and_no_employee_rights()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var driver = await app.User("driver@example.com");
        var a = await app.ApprovedOrganization("DR-A", "owner-a@example.com");
        var b = await app.ApprovedOrganization("DR-B", "owner-b@example.com");
        app.ActAs(driver);
        var profile = await app.Post("/drivers", new { fullName = "Driver A" });
        var id = profile.GetProperty("id").GetGuid();
        app.Admin();
        await app.Post($"/organizations/{a}/drivers/{id}/invitations", new { }, HttpStatusCode.Conflict);
        await app.Post($"/drivers/{id}/review", new { approved = true, note = "Reviewed driver documents." });
        await app.Post($"/organizations/{a}/drivers/{id}/invitations", new { });
        var tokenA = await app.InvitationToken(driver.Email);
        await app.Post($"/organizations/{b}/drivers/{id}/invitations", new { });
        var tokenB = await app.InvitationToken(driver.Email);
        app.ActAs(driver);
        await app.Post("/drivers/invitations/accept", new { token = tokenA });
        var engagement = await app.Post("/drivers/invitations/accept", new { token = tokenB });
        await app.Post("/drivers/invitations/accept", new { token = tokenA }, HttpStatusCode.Unauthorized);
        await app.Get($"/organizations/{a}/vehicles", HttpStatusCode.Forbidden);
        await app.Delete($"/organizations/{b}/drivers/{engagement.GetProperty("id").GetGuid()}");
        using var scope = app.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorDbContext>();
        Assert.Equal(2, await db.Engagements.CountAsync());
        Assert.Equal(1, await db.Engagements.CountAsync(x => x.AcceptedAt != null && x.EndedAt == null));
        Assert.Equal(0, await db.Members.CountAsync(x => x.AccountId == driver.Id));
    }

    [Fact]
    public async Task TC_FOUNDATION_004_Vehicle_has_one_operator_and_preserves_history()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var a = await app.ApprovedOrganization("VEH-A", "a@example.com");
        var b = await app.ApprovedOrganization("VEH-B", "b@example.com");
        var body = new { registrationPlate = "51A-123.45", description = "Test vehicle", verificationReference = "QA document review 1" };
        var first = await app.Post($"/organizations/{a}/vehicles", body);
        await app.Post($"/organizations/{b}/vehicles", body, HttpStatusCode.Conflict);
        var linkId = first.GetProperty("linkId").GetGuid();
        await app.Put($"/organizations/{a}/vehicles/{linkId}/suspension", new { isSuspended = true });
        await app.Post($"/organizations/{b}/vehicles", body, HttpStatusCode.Conflict);
        await app.Delete($"/organizations/{a}/vehicles/{linkId}");
        var second = await app.Post($"/organizations/{b}/vehicles", body);
        Assert.Equal(first.GetProperty("vehicleId").GetGuid(), second.GetProperty("vehicleId").GetGuid());
        var history = await app.Get($"/organizations/{a}/vehicles");
        Assert.NotEqual(JsonValueKind.Null, history[0].GetProperty("endedAt").ValueKind);
        using var scope = app.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorDbContext>();
        Assert.Equal(1, await db.Vehicles.CountAsync());
        Assert.Equal(2, await db.OrganizationVehicles.CountAsync());
    }

    [Fact]
    public async Task TC_FOUNDATION_005_Expired_and_replaced_invitations_are_rejected()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var owner = await app.User("expiry@example.com");
        var org = await app.ApprovedOrganization("EXPIRY", owner.Email);
        var oldToken = await app.InvitationToken(owner.Email);
        await app.Post($"/organizations/{org}/owner-invitation", new { });
        var newToken = await app.InvitationToken(owner.Email);
        app.ActAs(owner);
        await app.Post("/organizations/invitations/accept", new { token = oldToken }, HttpStatusCode.Unauthorized);
        using (var scope = app.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OperatorDbContext>();
            await db.Invitations.ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        }
        await app.Post("/organizations/invitations/accept", new { token = newToken }, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void TC_FOUNDATION_006_Migrations_preserve_accounts_and_enforce_active_vehicle_uniqueness()
    {
        using var identity = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=unused;Database=ScriptOnly;Integrated Security=true").Options);
        Assert.False(identity.Database.HasPendingModelChanges());
        var sql = identity.GetService<IMigrator>().GenerateScript("20261012120436_ActiveRolePermissionUniqueIndexes");
        Assert.Contains("IX_Users_AccountGroup_Email", sql);
        Assert.Contains("N'InternalAdmin'", sql);
        Assert.Contains("Resolve normalized email duplicates", sql);
        using var operators = new OperatorDbContext(new DbContextOptionsBuilder<OperatorDbContext>()
            .UseSqlServer("Server=unused;Database=ScriptOnly;Integrated Security=true").Options);
        Assert.False(operators.Database.HasPendingModelChanges());
        var operatorSql = operators.GetService<IMigrator>().GenerateScript();
        Assert.Contains("CREATE UNIQUE INDEX [IX_OrganizationVehicles_VehicleId]", operatorSql);
        Assert.Contains("WHERE [EndedAt] IS NULL", operatorSql);
        Assert.DoesNotContain("DROP TABLE", operatorSql);
    }

    [Fact]
    public async Task TC_FOUNDATION_007_Unresolved_obligations_block_detachment()
    {
        using var app = new Fixture(new BlockingObligations());
        await app.InitializeAsync();
        var organization = await app.ApprovedOrganization("BLOCK", "block-owner@example.com");
        var vehicle = await app.Post($"/organizations/{organization}/vehicles", new
        { registrationPlate = "51A99999", description = "", verificationReference = "QA reviewed." });
        await app.Delete($"/organizations/{organization}/vehicles/{vehicle.GetProperty("linkId").GetGuid()}", HttpStatusCode.Conflict);
        var driver = await app.User("blocked-driver@example.com");
        app.ActAs(driver);
        var profile = await app.Post("/drivers", new { fullName = "Blocked driver" });
        var driverId = profile.GetProperty("id").GetGuid();
        app.Admin();
        await app.Post($"/drivers/{driverId}/review", new { approved = true, note = "QA reviewed." });
        await app.Post($"/organizations/{organization}/drivers/{driverId}/invitations", new { });
        var token = await app.InvitationToken(driver.Email);
        app.ActAs(driver);
        var engagement = await app.Post("/drivers/invitations/accept", new { token });
        await app.Delete($"/organizations/{organization}/drivers/{engagement.GetProperty("id").GetGuid()}", HttpStatusCode.Conflict);
        using var scope = app.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorDbContext>();
        Assert.Null((await db.OrganizationVehicles.SingleAsync()).EndedAt);
        Assert.Null((await db.Engagements.SingleAsync()).EndedAt);
    }

    [Fact]
    public async Task TC_FOUNDATION_008_Logout_cannot_revoke_another_account_session()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var a = await app.Post("/auth/register", new
        { email = "logout@example.com", password = "123456", fullName = "A", accountGroup = "Customer" });
        var b = await app.Post("/auth/register", new
        { email = "logout@example.com", password = "123456", fullName = "B", accountGroup = "Operator" });
        app.Client.DefaultRequestHeaders.Authorization = new("Bearer", a.GetProperty("accessToken").GetString());
        await app.Post("/auth/logout", new { refreshToken = b.GetProperty("refreshToken").GetString() });
        var refreshed = await app.Post("/auth/refresh", new { refreshToken = b.GetProperty("refreshToken").GetString() });
        Assert.Equal(b.GetProperty("accountId").GetGuid(), refreshed.GetProperty("accountId").GetGuid());
    }

    [Fact]
    public async Task TC_FOUNDATION_009_Driver_quota_is_separate_and_pending_invites_reserve_capacity()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var org = await app.ApprovedOrganization("DRIVER-QUOTA", "quota-owner@example.com");
        await app.Put($"/organizations/{org}/driver-limit", new { limit = 1 });
        for (var i = 0; i < 2; i++)
        {
            var user = await app.User($"quota-driver-{i}@example.com");
            app.ActAs(user);
            var driver = await app.Post("/drivers", new { fullName = "Quota driver " + i });
            var id = driver.GetProperty("id").GetGuid();
            app.Admin();
            await app.Post($"/drivers/{id}/review", new { approved = true, note = "QA reviewed." });
            await app.Post($"/organizations/{org}/drivers/{id}/invitations", new { },
                i == 0 ? HttpStatusCode.OK : HttpStatusCode.Conflict);
        }
        using var scope = app.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorDbContext>();
        Assert.Equal(1, await db.Invitations.CountAsync()); // Owner invitation, not drivers.
        Assert.Equal(1, await db.Engagements.CountAsync());
    }

    [Fact]
    public async Task TC_FOUNDATION_010_Invite_driver_before_registration_requires_verified_profile_and_consent()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var org = await app.ApprovedOrganization("NEW-DRIVER", "new-owner@example.com");
        await app.Put($"/organizations/{org}/driver-limit", new { limit = 1 });
        await app.Post($"/organizations/{org}/driver-invitations", new { email = "new-driver@example.com" });
        var token = await app.InvitationToken("new-driver@example.com");
        await app.Post($"/organizations/{org}/driver-invitations", new { email = "another-driver@example.com" }, HttpStatusCode.Conflict);
        var user = await app.User("new-driver@example.com");
        app.ActAs(user);
        await app.Post("/organizations/invitations/accept", new { token }, HttpStatusCode.Unauthorized);
        await app.Post("/drivers/invitations/accept", new { token }, HttpStatusCode.Conflict);
        var driver = await app.Post("/drivers", new { fullName = "New driver" });
        await app.Post("/drivers/invitations/accept", new { token }, HttpStatusCode.Conflict);
        app.Admin();
        await app.Post($"/drivers/{driver.GetProperty("id").GetGuid()}/review", new { approved = true, note = "Reviewed." });
        app.ActAs(user);
        await app.Post("/drivers/invitations/accept", new { token });
        await app.Post("/drivers/invitations/accept", new { token }, HttpStatusCode.Unauthorized);
        await app.Get($"/organizations/{org}/members", HttpStatusCode.Forbidden);
    }

    private sealed class BlockingObligations : TransConnect.Common.Transport.ITransportObligations
    {
        public Task<bool> HasDriverObligationsAsync(Guid organizationId, Guid driverId, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> HasVehicleObligationsAsync(Guid organizationId, Guid vehicleId, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class Fixture : IDisposable
    {
        public ApiFactory Factory { get; }
        public HttpClient Client { get; }
        private User _admin = null!;
        public Fixture(TransConnect.Common.Transport.ITransportObligations? obligations = null)
        {
            Factory = new ApiFactory(obligations: obligations);
            Client = Factory.CreateApiClient();
        }

        public async Task InitializeAsync()
        {
            using var scope = Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreatedAsync();
            await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.EnsureCreatedAsync();
            await scope.ServiceProvider.GetRequiredService<OperatorDbContext>().Database.EnsureCreatedAsync();
            _admin = await User("internal@example.com", AccountGroups.InternalAdmin);
        }

        public async Task<User> User(string email, string group = AccountGroups.Operator)
        {
            using var scope = Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = new User { Email = email, AccountGroup = group, FullName = email, EmailVerifiedAt = DateTime.UtcNow };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user;
        }

        public void ActAs(User user)
        {
            using var scope = Factory.Services.CreateScope();
            var token = scope.ServiceProvider.GetRequiredService<ITokenService>().GenerateAccessToken(user, [],
                user.AccountGroup == AccountGroups.InternalAdmin ? ["organizations.manage"] : []).AccessToken;
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        public void Admin() => ActAs(_admin);
        public async Task<Guid> Organization(string number, string owner)
        {
            var data = await Post("/organizations", new { name = number, registrationNumber = number, ownerEmail = owner });
            return data.GetProperty("id").GetGuid();
        }
        public async Task<Guid> ApprovedOrganization(string number, string owner)
        {
            var id = await Organization(number, owner);
            Admin();
            await Post($"/organizations/{id}/review", new { approved = true, note = "QA reviewed." });
            return id;
        }
        public async Task<string> InvitationToken(string email)
        {
            using var scope = Factory.Services.CreateScope();
            var body = await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().EmailMessages
                .Where(x => x.ToEmail == email && x.TemplateCode == "organization-invitation")
                .OrderByDescending(x => x.CreatedAt).Select(x => x.BodyText).FirstAsync();
            return Regex.Match(body, "[A-F0-9]{64}").Value;
        }
        public async Task<string> LatestOtp(string email)
        {
            using var scope = Factory.Services.CreateScope();
            var body = await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().EmailMessages
                .Where(x => x.ToEmail == email && x.TemplateCode == "password-reset")
                .OrderByDescending(x => x.CreatedAt).Select(x => x.BodyText).FirstAsync();
            return Regex.Match(body, @"\b\d{6}\b").Value;
        }
        public async Task<JsonElement> Post(string url, object body, HttpStatusCode status = HttpStatusCode.OK) =>
            await Check(await Client.PostAsJsonAsync(url, body), status);
        public async Task<JsonElement> Put(string url, object body, HttpStatusCode status = HttpStatusCode.OK) =>
            await Check(await Client.PutAsJsonAsync(url, body), status);
        public async Task<JsonElement> Get(string url, HttpStatusCode status = HttpStatusCode.OK) =>
            await Check(await Client.GetAsync(url), status);
        public async Task<JsonElement> Delete(string url, HttpStatusCode status = HttpStatusCode.OK) =>
            await Check(await Client.DeleteAsync(url), status);
        private static async Task<JsonElement> Check(HttpResponseMessage response, HttpStatusCode status)
        {
            using (response)
            {
                var body = await response.Content.ReadAsStringAsync();
                Assert.True(response.StatusCode == status, $"Expected {status}, got {response.StatusCode}: {body}");
                using var json = JsonDocument.Parse(body);
                return json.RootElement.GetProperty("data").Clone();
            }
        }
        public void Dispose() { Client.Dispose(); Factory.Dispose(); }
    }
}
