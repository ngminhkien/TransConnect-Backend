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
    public async Task TC_CLOSURE_008_Search_context_and_member_names_are_scoped_and_paged()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var owner = await app.User("paged-owner@example.com");
        var other = await app.User("paged-other@example.com");
        var org = await app.ApprovedOrganization("PAGE-ORG", owner.Email);
        app.ActAs(owner);
        await app.Post("/organizations/invitations/accept", new { token = await app.InvitationToken(owner.Email) });
        await app.Post($"/organizations/{org}/invitations", new { email = other.Email, role = "Dispatcher" });
        var context = await app.Get("/operator/context");
        var membership = context.GetProperty("memberships")[0];
        Assert.Equal(2, membership.GetProperty("employeeOccupancy").GetInt32());
        Assert.Contains("owner", membership.GetProperty("capabilities").EnumerateArray().Select(x => x.GetString()));
        var names = await app.Get($"/organizations/{org}/members/search?pageNumber=1&pageSize=1");
        Assert.Equal(owner.Email, names.GetProperty("items")[0].GetProperty("fullName").GetString());
        Assert.Equal(1, names.GetProperty("totalRecords").GetInt32());
        var invitations = await app.Get($"/organizations/{org}/invitations");
        Assert.DoesNotContain("tokenHash", invitations.GetRawText());
        Assert.DoesNotContain("invitationToken", invitations.GetRawText());
        await app.Get("/organizations/search?pageSize=0", HttpStatusCode.BadRequest);
        app.ActAs(other);
        Assert.Equal(0, (await app.Get("/organizations/search")).GetProperty("totalRecords").GetInt32());
        await app.Get($"/organizations/{org}/members/search", HttpStatusCode.Forbidden);
        await app.Get("/drivers/search", HttpStatusCode.Forbidden);
        await app.Post("/drivers", new { fullName = "Driver identity" });
        await app.Put("/auth/me", new { fullName = "Account display", phoneNumber = "0901234567" });
        Assert.Equal("Driver identity", (await app.Get("/drivers/me")).GetProperty("fullName").GetString());
        app.Admin();
        var drivers = await app.Get("/drivers/search?status=Pending&pageSize=1");
        Assert.Equal(1, drivers.GetProperty("totalRecords").GetInt32());
        var driverId = drivers.GetProperty("items")[0].GetProperty("id").GetGuid();
        await app.Post($"/drivers/{driverId}/review", new { approved = true, note = "Reviewed" });
        Assert.Equal("Approved", (await app.Get($"/drivers/{driverId}")).GetProperty("reviewStatus").GetString());
        Assert.Equal(1, (await app.Get("/drivers/search?status=Approved")).GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task TC_CLOSURE_009_Applicant_identity_is_server_owned_and_tax_format_is_validated()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        await app.Post("/organizations", new { name = "Public", registrationNumber = "0123456789" }, HttpStatusCode.Unauthorized);
        var applicant = await app.User("tax-applicant@example.com");
        var other = await app.User("spoof-owner@example.com");
        app.ActAs(applicant);
        await app.Post("/organizations", new { name = "Bad", registrationNumber = "ABCDEFGHIJ" }, HttpStatusCode.BadRequest);
        var org = await app.Post("/organizations", new {
            name = "Good", registrationNumber = "0123456789001", createdByUserId = other.Id, ownerEmail = other.Email
        });
        Assert.Equal(applicant.Id, org.GetProperty("createdByUserId").GetGuid());
        Assert.Equal(applicant.Email, org.GetProperty("ownerEmail").GetString());
        var orgId = org.GetProperty("id").GetGuid();
        app.Admin();
        await app.Post($"/organizations/{orgId}/review", new { approved = false, note = " " }, HttpStatusCode.BadRequest);
        app.ActAs(other);
        await app.Get($"/organizations/{orgId}", HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TC_CLOSURE_010_Legacy_admin_resubmit_keeps_invitation_path_and_audit_reason_is_readable()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var owner = await app.User("legacy-owner@example.com");
        var org = await app.Organization("LEGACY", owner.Email);
        app.Admin();
        await app.Post($"/organizations/{org}/review", new { approved = false, note = "Legacy fix" });
        await app.Post($"/organizations/{org}/resubmit", new {
            name = "Fixed legacy", registrationNumber = "0123456789", ownerEmail = owner.Email
        });
        await app.Post($"/organizations/{org}/review", new { approved = true, note = "Legacy accepted" });
        Assert.NotEmpty(await app.InvitationToken(owner.Email));
        await app.Put($"/accounts/{owner.Id}/status", new { isActive = false, reason = "Review pending" });
        var audit = await app.Get($"/accounts/{owner.Id}/audit");
        Assert.Equal("Review pending", audit.GetProperty("items")[0].GetProperty("reason").GetString());
        Assert.False(audit.GetProperty("items")[0].GetProperty("isActive").GetBoolean());
        Assert.False(audit.GetProperty("items")[0].TryGetProperty("changesJson", out _));
    }

    [Fact]
    public async Task TC_CLOSURE_001_Applicant_resubmits_and_becomes_owner_without_invitation()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var applicant = await app.User("applicant@example.com");
        var stranger = await app.User("other@example.com");
        app.ActAs(applicant);
        var org = await app.Post("/organizations", new { name = "Applicant", registrationNumber = "0123456789" });
        var id = org.GetProperty("id").GetGuid();
        app.ActAs(stranger);
        await app.Get($"/organizations/{id}", HttpStatusCode.Forbidden);
        await app.Post($"/organizations/{id}/resubmit", new { name = "Bad", registrationNumber = "0123456789" }, HttpStatusCode.NotFound);
        app.Admin();
        await app.Post($"/organizations/{id}/review", new { approved = false, note = "Sửa tên" });
        app.ActAs(applicant);
        var retry = await app.Post($"/organizations/{id}/resubmit", new { name = "Corrected", registrationNumber = "0123456789" });
        Assert.Equal(id, retry.GetProperty("id").GetGuid());
        app.Admin();
        await app.Post($"/organizations/{id}/review", new { approved = true, note = "Đã kiểm tra" });
        app.ActAs(applicant);
        var context = await app.Get("/operator/context");
        Assert.Equal("OrganizationOwner", context.GetProperty("memberships")[0].GetProperty("role").GetString());
        Assert.Equal(3, (await app.Get($"/organizations/{id}/reviews")).GetArrayLength());
        using var scope = app.Factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<OperatorDbContext>().Invitations.AnyAsync(x => x.OrganizationId == id));
    }

    [Fact]
    public async Task TC_CLOSURE_002_Driver_resubmission_preserves_id_history()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var driver = await app.User("resubmit-driver@example.com");
        app.ActAs(driver);
        var id = (await app.Post("/drivers", new { fullName = "First" })).GetProperty("id").GetGuid();
        await app.Post("/drivers/me/resubmit", new { fullName = "Second" }, HttpStatusCode.Conflict);
        app.Admin();
        await app.Post($"/drivers/{id}/review", new { approved = false, note = "Sai tên" });
        app.ActAs(driver);
        var retry = await app.Post("/drivers/me/resubmit", new { fullName = "Second" });
        Assert.Equal(id, retry.GetProperty("id").GetGuid());
        Assert.Equal("Pending", retry.GetProperty("reviewStatus").GetString());
        Assert.Equal(2, (await app.Get($"/drivers/{id}/reviews")).GetArrayLength());
    }

    [Fact]
    public async Task TC_CLOSURE_003_Lock_and_permission_removal_apply_to_existing_access_tokens()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var user = await app.User("locked@example.com", AccountGroups.Customer);
        app.Admin();
        await app.Put($"/accounts/{user.Id}/status", new { isActive = false, reason = "Test lock" });
        app.ActAs(user);
        await app.Get("/auth/me", HttpStatusCode.Unauthorized);
        app.Admin();
        await app.Put($"/accounts/{user.Id}/status", new { isActive = true, reason = "Test unlock" });
        app.ActAs(user);
        await app.Get("/auth/me");
        app.Admin();
        using var scope = app.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.RolePermissions.Where(x => x.Permission.Code == "users.manage").ExecuteDeleteAsync();
        await app.Get("/accounts", HttpStatusCode.Forbidden);
        Assert.Equal(2, await db.RbacAuditLogs.CountAsync(x => x.Action == "AccountStatus"));
    }
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
        var permissionChange = audit.EnumerateArray().Single(x => x.GetProperty("action").GetString() == "member.update");
        Assert.Equal("OrganizationMember", permissionChange.GetProperty("subjectType").GetString());
        Assert.False(string.IsNullOrWhiteSpace(permissionChange.GetProperty("requestId").GetString()));
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
        using var scope = app.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorDbContext>();
        var accepted = await db.Invitations.SingleAsync(x => x.Role == "Driver" && x.AcceptedAt != null);
        Assert.Equal(accepted.Id, (await db.Engagements.SingleAsync()).InvitationId);
    }

    [Fact]
    public async Task TC_FOUNDATION_011_Owner_and_driver_require_separate_accounts_in_both_directions()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var owner = await app.User("separate-owner@example.com");
        var org = await app.ApprovedOrganization("SEPARATE", owner.Email);
        var token = await app.InvitationToken(owner.Email);
        app.ActAs(owner);
        await app.Post("/organizations/invitations/accept", new { token });
        await app.Post("/drivers", new { fullName = "Owner cannot drive with this account" }, HttpStatusCode.Conflict);

        var driver = await app.User("separate-driver@example.com");
        app.ActAs(driver);
        await app.Post("/drivers", new { fullName = "Independent driver" });
        var other = await app.ApprovedOrganization("DRIVER-OWNER", driver.Email);
        var driverToken = await app.InvitationToken(driver.Email);
        app.ActAs(driver);
        await app.Post("/organizations/invitations/accept", new { token = driverToken }, HttpStatusCode.Conflict);
        using var scope = app.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorDbContext>();
        Assert.False(await db.Members.AnyAsync(x => x.AccountId == driver.Id));
        Assert.Null((await db.Invitations.SingleAsync(x => x.OrganizationId == other)).AcceptedAt);
        Assert.False(await db.Drivers.AnyAsync(x => x.AccountId == owner.Id));
    }

    [Fact]
    public async Task TC_FOUNDATION_012_Vehicle_end_requires_reason_and_preserves_first_actor_and_reason()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var org = await app.ApprovedOrganization("END-REASON", "reason-owner@example.com");
        var vehicle = await app.Post($"/organizations/{org}/vehicles", new
        { registrationPlate = "51A77777", description = "", verificationReference = "Reviewed" });
        var id = vehicle.GetProperty("linkId").GetGuid();
        var url = $"/organizations/{org}/vehicles/{id}";
        using var invalid = await app.Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url)
        { Content = JsonContent.Create(new { }) });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        await app.Delete(url);
        using var scope = app.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorDbContext>();
        var before = await db.OrganizationVehicles.AsNoTracking().SingleAsync();
        Assert.Equal("End test operating agreement", before.EndReason);
        Assert.NotNull(before.EndedBy);
        using var again = await app.Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url)
        { Content = JsonContent.Create(new { reason = "Do not overwrite" }) });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var after = await db.OrganizationVehicles.AsNoTracking().SingleAsync();
        Assert.Equal(before.EndedBy, after.EndedBy);
        Assert.Equal(before.EndedAt, after.EndedAt);
        Assert.Equal(before.EndReason, after.EndReason);
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.Action == "vehicle.end"));
        var history = await app.Get($"/organizations/{org}/vehicles");
        Assert.Equal(before.EndReason, history[0].GetProperty("endReason").GetString());
        Assert.Equal(before.EndedBy, history[0].GetProperty("endedBy").GetGuid());
    }

    [Fact]
    public async Task TC_FOUNDATION_014_Database_rejects_invalid_states_and_duplicate_active_owners()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var owner = await app.User("constraint-owner@example.com");
        var org = await app.ApprovedOrganization("CONSTRAINTS", owner.Email);
        var token = await app.InvitationToken(owner.Email);
        app.ActAs(owner);
        await app.Post("/organizations/invitations/accept", new { token });
        using var scope = app.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorDbContext>();
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE Organizations SET ReviewStatus = 'Unknown'"));
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE Members SET Role = 'Driver'"));
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE Invitations SET Role = 'Unknown'"));
        db.Members.Add(new TransConnect.Modules.Operator.Entities.OrganizationMember
        { OrganizationId = org, AccountId = Guid.NewGuid(), Role = "OrganizationOwner" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(1, await db.Members.CountAsync(x => x.IsActive && x.Role == "OrganizationOwner"));
        // Manager is not accidentally excluded by the filtered Owner index.
        db.Members.Add(new TransConnect.Modules.Operator.Entities.OrganizationMember
        { OrganizationId = org, AccountId = Guid.NewGuid(), Role = "Manager" });
        await db.SaveChangesAsync();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => identity.Database.ExecuteSqlRawAsync(
            "UPDATE Users SET AccountGroup = 'Driver'"));
        var notification = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => notification.Database.ExecuteSqlRawAsync(
            "UPDATE EmailMessages SET Status = 99"));
    }

    [Fact]
    public async Task TC_FOUNDATION_013_Concurrent_owner_accept_and_driver_submit_cannot_both_succeed()
    {
        var path = Path.Combine(Path.GetTempPath(), $"operator-race-{Guid.NewGuid():N}.db");
        try
        {
            using var app = new Fixture(operatorDatabasePath: path);
            await app.InitializeAsync();
            var user = await app.User("race-owner@example.com");
            await app.ApprovedOrganization("ACCOUNT-RACE", user.Email);
            var token = await app.InvitationToken(user.Email);
            app.ActAs(user);
            var responses = await Task.WhenAll(
                Task.Run(() => app.Client.PostAsJsonAsync("/organizations/invitations/accept", new { token })),
                Task.Run(() => app.Client.PostAsJsonAsync("/drivers", new { fullName = "Concurrent driver" })));
            try
            {
                Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.OK));
                Assert.Single(responses.Where(x => x.StatusCode == HttpStatusCode.Conflict));
                using var scope = app.Factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OperatorDbContext>();
                Assert.Equal(1, await db.Members.CountAsync(x => x.AccountId == user.Id)
                    + await db.Drivers.CountAsync(x => x.AccountId == user.Id));
            }
            finally { foreach (var response in responses) response.Dispose(); }
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(path + suffix);
        }
    }

    [Fact]
    public async Task TC_FOUNDATION_015_Minimal_profiles_are_optional_for_legacy_data_and_owner_updates_validate_units()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var owner = await app.User("profile-owner@example.com");
        var org = await app.ApprovedOrganization("PROFILES", owner.Email);
        var vehicle = await app.Post($"/organizations/{org}/vehicles", new
        { registrationPlate = "51A88888", description = "", verificationReference = "Synthetic review" });
        var linkId = vehicle.GetProperty("linkId").GetGuid();
        var token = await app.InvitationToken(owner.Email);
        app.ActAs(owner);
        await app.Post("/organizations/invitations/accept", new { token });
        Assert.False((await app.Get($"/organizations/{org}")).GetProperty("isProfileComplete").GetBoolean());
        var updated = await app.Put($"/organizations/{org}/profile", new
        { legalEntityType = "HouseholdBusiness", registeredAddress = "Synthetic address", contactPhone = "0900000000", contactEmail = "CONTACT@example.com" });
        Assert.True(updated.GetProperty("isProfileComplete").GetBoolean());
        Assert.Equal("contact@example.com", updated.GetProperty("contactEmail").GetString());
        Assert.Equal(owner.Email, updated.GetProperty("ownerEmail").GetString());
        await app.Put($"/organizations/{org}/vehicles/{linkId}/profile", new { vehicleKind = "Passenger", seatCount = 16 });
        await app.Put($"/organizations/{org}/vehicles/{linkId}/profile", new { vehicleKind = "Passenger", seatCount = 16, permittedPayloadKg = 100 }, HttpStatusCode.BadRequest);
        await app.Put($"/organizations/{org}/vehicles/{linkId}/profile", new { vehicleKind = "Freight", permittedPayloadKg = -1 }, HttpStatusCode.BadRequest);
        var freight = await app.Put($"/organizations/{org}/vehicles/{linkId}/profile", new { vehicleKind = "Freight", permittedPayloadKg = 2500.50 });
        Assert.True(freight.GetProperty("isProfileComplete").GetBoolean());
        Assert.Equal(2500.50m, freight.GetProperty("permittedPayloadKg").GetDecimal());
        await app.Put($"/organizations/{org}/profile", new { legalEntityType = "Other" }, HttpStatusCode.BadRequest);
        var stranger = await app.User("profile-stranger@example.com");
        app.ActAs(stranger);
        await app.Put($"/organizations/{org}/vehicles/{linkId}/profile", new { vehicleKind = "Passenger", seatCount = 10 }, HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task TC_FOUNDATION_016_Ended_driver_contact_is_a_snapshot_and_only_current_owner_can_read_it()
    {
        using var app = new Fixture();
        await app.InitializeAsync();
        var owner = await app.User("snapshot-owner@example.com");
        var manager = await app.User("snapshot-manager@example.com");
        var driver = await app.User("snapshot-driver@example.com");
        var org = await app.ApprovedOrganization("SNAPSHOT", owner.Email);
        var ownerToken = await app.InvitationToken(owner.Email);
        app.ActAs(owner);
        await app.Post("/organizations/invitations/accept", new { token = ownerToken });
        await app.Post($"/organizations/{org}/invitations", new { email = manager.Email, role = "Manager" });
        var managerToken = await app.InvitationToken(manager.Email);
        app.ActAs(manager);
        await app.Post("/organizations/invitations/accept", new { token = managerToken });
        app.ActAs(driver);
        var profile = await app.Post("/drivers", new { fullName = "Synthetic Driver" });
        Assert.False(profile.GetProperty("isProfileComplete").GetBoolean());
        var driverId = profile.GetProperty("id").GetGuid();
        await app.Put("/drivers/me/profile", new { contactPhone = "0900000001", contactAddress = "Old contact address" });
        app.Admin();
        await app.Post($"/drivers/{driverId}/review", new { approved = true, note = "Synthetic review" });
        await app.Post($"/organizations/{org}/drivers/{driverId}/invitations", new { });
        var token = await app.InvitationToken(driver.Email);
        app.ActAs(driver);
        var engagement = await app.Post("/drivers/invitations/accept", new { token });
        var engagementId = engagement.GetProperty("id").GetGuid();
        var route = $"/organizations/{org}/drivers/{engagementId}/profile";
        app.ActAs(owner);
        Assert.False((await app.Get(route)).GetProperty("isHistorical").GetBoolean());
        // Defensive legacy-data case: a revoked engagement never authorizes live contact access.
        using (var revokedScope = app.Factory.Services.CreateScope())
        {
            var revokedDb = revokedScope.ServiceProvider.GetRequiredService<OperatorDbContext>();
            await revokedDb.Engagements.Where(x => x.Id == engagementId).ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, DateTime.UtcNow));
            await app.Get(route, HttpStatusCode.NotFound);
            await revokedDb.Engagements.Where(x => x.Id == engagementId).ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAt, (DateTime?)null));
        }
        app.ActAs(manager);
        await app.Get(route, HttpStatusCode.Forbidden);
        app.ActAs(driver);
        await app.Delete($"/organizations/{org}/drivers/{engagementId}");
        await app.Put("/drivers/me/profile", new { contactPhone = "0900000002", contactAddress = "New private address" });
        await app.Delete($"/organizations/{org}/drivers/{engagementId}");
        app.ActAs(owner);
        var snapshot = await app.Get(route);
        Assert.True(snapshot.GetProperty("isHistorical").GetBoolean());
        Assert.True(snapshot.GetProperty("snapshotAvailable").GetBoolean());
        Assert.Equal("0900000001", snapshot.GetProperty("contactPhone").GetString());
        Assert.Equal("Old contact address", snapshot.GetProperty("contactAddress").GetString());
        using var scope = app.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OperatorDbContext>();
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.Action == "driver.end"));
        await db.Members.Where(x => x.AccountId == owner.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        await app.Get(route, HttpStatusCode.Forbidden);
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
        public Fixture(TransConnect.Common.Transport.ITransportObligations? obligations = null, string? operatorDatabasePath = null)
        {
            Factory = new ApiFactory(obligations: obligations, operatorDatabasePath: operatorDatabasePath);
            Client = Factory.CreateApiClient();
        }

        public async Task InitializeAsync()
        {
            using var scope = Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreatedAsync();
            await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.EnsureCreatedAsync();
            await scope.ServiceProvider.GetRequiredService<OperatorDbContext>().Database.EnsureCreatedAsync();
            _admin = await User("internal@example.com", AccountGroups.InternalAdmin);
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var role = new Role { Name = "FoundationAdmin" };
            identity.Roles.Add(role);
            identity.UserRoles.Add(new UserRole { UserId = _admin.Id, RoleId = role.Id });
            foreach (var code in new[] { "organizations.manage", "users.manage", "admins.manage" })
            {
                var permission = new Permission { Code = code };
                identity.Permissions.Add(permission);
                identity.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            }
            await identity.SaveChangesAsync();
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
            Admin();
            var digits = (uint)System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(number)));
            var data = await Post("/organizations", new { name = number, registrationNumber = digits.ToString("D10"), ownerEmail = owner });
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
            await Check(await Client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, url)
            {
                Content = url.Contains("/vehicles/") ? JsonContent.Create(new { reason = "End test operating agreement" }) : null
            }), status);
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
