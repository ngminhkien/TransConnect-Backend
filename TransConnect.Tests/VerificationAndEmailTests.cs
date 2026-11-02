using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Enums;
using TransConnect.Modules.Identity.Options;
using TransConnect.Modules.Identity.Persistence;
using TransConnect.Modules.Identity.Services;
using TransConnect.Modules.Notification.Enums;
using TransConnect.Modules.Notification.Persistence;
using Xunit;

namespace TransConnect.Tests;

public class VerificationAndEmailTests
{
    [Fact]
    public async Task TC_OTP_001_Codes_are_hashed_purpose_scoped_and_single_use()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = await AuthLifecycleTests.SeedUser(scope.ServiceProvider);
        var codes = scope.ServiceProvider.GetRequiredService<IVerificationCodeService>();
        var first = await codes.IssueAsync(user.Id, VerificationPurpose.EmailVerification);
        var oldId = (await db.VerificationCodes.SingleAsync()).Id;
        var second = await codes.IssueAsync(user.Id, VerificationPurpose.EmailVerification);
        Assert.True((await db.VerificationCodes.AsNoTracking().SingleAsync(v => v.Id == oldId)).IsUsed);
        var active = await db.VerificationCodes.SingleAsync(v => !v.IsUsed);
        Assert.NotEqual(second, active.CodeHash);
        Assert.True(scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Verify(second, active.CodeHash));
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => codes.ConsumeAsync(user.Id, VerificationPurpose.PasswordReset, second));
        await codes.ConsumeAsync(user.Id, VerificationPurpose.EmailVerification, second);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => codes.ConsumeAsync(user.Id, VerificationPurpose.EmailVerification, second));
    }

    [Fact]
    public async Task TC_OTP_002_Expiry_attempt_and_request_limits_are_enforced()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = await AuthLifecycleTests.SeedUser(scope.ServiceProvider);
        var codes = scope.ServiceProvider.GetRequiredService<IVerificationCodeService>();
        var limits = scope.ServiceProvider.GetRequiredService<IOptions<VerificationOptions>>().Value;
        var expired = await codes.IssueAsync(user.Id, VerificationPurpose.EmailVerification);
        (await db.VerificationCodes.SingleAsync()).ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => codes.ConsumeAsync(user.Id, VerificationPurpose.EmailVerification, expired));
        var current = await codes.IssueAsync(user.Id, VerificationPurpose.EmailVerification);
        for (var i = 0; i < limits.MaxAttempts; i++)
            await Assert.ThrowsAsync<UnauthorizedAppException>(() => codes.ConsumeAsync(user.Id, VerificationPurpose.EmailVerification, "not-a-numeric-code"));
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => codes.ConsumeAsync(user.Id, VerificationPurpose.EmailVerification, current));
        for (var i = 2; i < limits.MaxRequestsPerHour; i++)
            await codes.IssueAsync(user.Id, VerificationPurpose.EmailVerification);
        await Assert.ThrowsAsync<TooManyRequestsException>(() => codes.IssueAsync(user.Id, VerificationPurpose.EmailVerification));
    }

    [Fact]
    public async Task TC_EMAIL_001_Register_queues_encoded_email_and_assigns_default_role()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var mailDb = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await db.Database.EnsureCreatedAsync();
        await mailDb.Database.EnsureCreatedAsync();
        db.Roles.Add(new Role { Name = "User" });
        await db.SaveChangesAsync();
        using var response = await client.PostAsJsonAsync("/auth/register", new
        {
            email = "register@example.com", password = "password123", fullName = "<script>alert(1)</script>"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(await db.UserRoles.ToListAsync());
        Assert.Single(await db.VerificationCodes.ToListAsync());
        var mail = await mailDb.EmailMessages.SingleAsync();
        Assert.Equal(TransConnect.Common.Notifications.EmailSourceTypes.VerificationCode, mail.SourceType);
        Assert.Equal((await db.VerificationCodes.SingleAsync()).Id, mail.SourceId);
        Assert.Equal("Otp", mail.RetentionCategory);
        Assert.NotNull(mail.DeliverBefore);
        Assert.Equal(NotificationStatus.Pending, mail.Status);
        Assert.DoesNotContain("<script>", mail.BodyHtml);
        Assert.Contains("&lt;script&gt;", mail.BodyHtml);
        Assert.Contains("<script>", mail.BodyText);
    }

    [Fact]
    public async Task TC_OTP_003_Forgot_password_does_not_reveal_unknown_or_rate_limited_email()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var mailDb = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await db.Database.EnsureCreatedAsync();
        await mailDb.Database.EnsureCreatedAsync();
        var user = await AuthLifecycleTests.SeedUser(scope.ServiceProvider);
        using var unknown = await client.PostAsJsonAsync("/auth/forgot-password", new { email = "unknown@example.com" });
        var expected = await unknown.Content.ReadAsStringAsync();
        var limit = scope.ServiceProvider.GetRequiredService<IOptions<VerificationOptions>>().Value.MaxRequestsPerHour;
        for (var i = 0; i <= limit; i++)
        {
            using var response = await client.PostAsJsonAsync("/auth/forgot-password", new { email = user.Email });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(expected, await response.Content.ReadAsStringAsync());
        }
        Assert.Equal(limit, await mailDb.EmailMessages.CountAsync());
    }
}
