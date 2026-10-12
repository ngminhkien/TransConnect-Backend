using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Persistence;
using TransConnect.Modules.Identity.Services;
using Xunit;

namespace TransConnect.Tests;

public class AuthLifecycleTests
{
    [Theory]
    [InlineData("inactive")]
    [InlineData("deleted")]
    public async Task TC_IAM_003_Refresh_rejects_unavailable_user(string state)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = await SeedUser(scope.ServiceProvider);
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var session = await auth.LoginAsync(new LoginRequest { Email = user.Email, Password = "password123" });
        user.IsActive = state != "inactive";
        user.DeletedAt = state == "deleted" ? DateTime.UtcNow : null;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => auth.RefreshAsync(new RefreshTokenRequest { RefreshToken = session.RefreshToken }));
        Assert.Equal(1, await db.RefreshTokens.CountAsync());
        Assert.False((await db.RefreshTokens.AsNoTracking().SingleAsync()).IsUsed);
    }

    [Fact]
    public async Task TC_IAM_004_Tokens_are_hashed_rotated_and_replay_revokes_sessions()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = await SeedUser(scope.ServiceProvider);
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var first = await auth.LoginAsync(new LoginRequest { Email = user.Email, Password = "password123" });
        Assert.DoesNotContain(await db.RefreshTokens.ToListAsync(), t => t.Token == first.RefreshToken);
        var digest = (await db.RefreshTokens.AsNoTracking().SingleAsync()).Token;
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => auth.RefreshAsync(new RefreshTokenRequest { RefreshToken = digest }));
        var second = await auth.RefreshAsync(new RefreshTokenRequest { RefreshToken = first.RefreshToken });
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => auth.RefreshAsync(new RefreshTokenRequest { RefreshToken = first.RefreshToken }));
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => auth.RefreshAsync(new RefreshTokenRequest { RefreshToken = second.RefreshToken }));
    }

    [Fact]
    public async Task TC_IAM_005_Logout_revokes_token_and_bad_password_is_rejected()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = await SeedUser(scope.ServiceProvider);
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => auth.LoginAsync(new LoginRequest { Email = user.Email, Password = "wrong" }));
        var session = await auth.LoginAsync(new LoginRequest { Email = user.Email, Password = "password123" });
        await auth.LogoutAsync(session.RefreshToken);
        await auth.LogoutAsync(session.RefreshToken);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => auth.RefreshAsync(new RefreshTokenRequest { RefreshToken = session.RefreshToken }));
    }

    [Fact]
    public async Task TC_IAM_006_Reset_password_consumes_otp_and_revokes_sessions()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync();
        var user = await SeedUser(scope.ServiceProvider);
        var auth = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var session = await auth.LoginAsync(new LoginRequest { Email = user.Email, Password = "password123" });
        var codes = scope.ServiceProvider.GetRequiredService<IVerificationCodeService>();
        var code = await codes.IssueAsync(user.Id, VerificationPurpose.PasswordReset);
        await auth.ResetPasswordAsync(new ResetPasswordRequest { Email = user.Email, Code = code, NewPassword = "new-password123" });
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => auth.LoginAsync(new LoginRequest { Email = user.Email, Password = "password123" }));
        Assert.True((await auth.LoginAsync(new LoginRequest { Email = user.Email, Password = "new-password123" })).EmailVerified);
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => auth.RefreshAsync(new RefreshTokenRequest { RefreshToken = session.RefreshToken }));
        await Assert.ThrowsAsync<UnauthorizedAppException>(() => codes.ConsumeAsync(user.Id, VerificationPurpose.PasswordReset, code));
    }

    internal static async Task<User> SeedUser(IServiceProvider services)
    {
        var db = services.GetRequiredService<IdentityDbContext>();
        var user = new User { Email = "lifecycle@example.com", FullName = "Lifecycle", PasswordHash = services.GetRequiredService<IPasswordHasher>().Hash("password123") };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}
