using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Persistence;
using TransConnect.Modules.Identity.Services;
using Xunit;

namespace TransConnect.Tests;

public class RefreshConcurrencyTests
{
    [Fact]
    public async Task TC_IAM_007_Concurrent_refresh_cannot_issue_two_successors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"transconnect-refresh-{Guid.NewGuid():N}.db");
        try
        {
            var gate = new RefreshReadGate();
            using var factory = new ApiFactory(path, gate);
            using var client = factory.CreateApiClient();
            string token;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                await db.Database.EnsureCreatedAsync();
                var user = await AuthLifecycleTests.SeedUser(scope.ServiceProvider);
                token = (await scope.ServiceProvider.GetRequiredService<IAuthService>().LoginAsync(
                    new LoginRequest { Email = user.Email, Password = "password123" })).RefreshToken;
            }
            gate.Enabled = true;
            async Task<bool> Refresh()
            {
                using var scope = factory.Services.CreateScope();
                try
                {
                    await scope.ServiceProvider.GetRequiredService<IAuthService>().RefreshAsync(new RefreshTokenRequest { RefreshToken = token });
                    return true;
                }
                catch (UnauthorizedAppException) { return false; }
            }
            var outcomes = await Task.WhenAll(Task.Run(Refresh), Task.Run(Refresh)).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Single(outcomes.Where(success => success));
            using var verificationScope = factory.Services.CreateScope();
            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            Assert.Equal(2, await verificationDb.RefreshTokens.CountAsync());
            Assert.False(await verificationDb.RefreshTokens.AnyAsync(t => !t.IsRevoked));
        }
        finally
        {
            foreach (var suffix in new[] { "", "-wal", "-shm" })
                File.Delete(path + suffix);
        }
    }

    // Both requests must read the same unused row before either can update it.
    private sealed class RefreshReadGate : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("FROM \"RefreshTokens\"") && command.CommandText.Contains("LIMIT 1"))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}
