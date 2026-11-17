using Xunit;
using TransConnect.Host.Commands;

namespace TransConnect.Tests;

public sealed class SeedTransportCommandTests
{
    [Fact]
    public void TC_SEAT_SEED_CLI_001_parses_command_and_keeps_host_args()
    {
        Assert.True(SeedTransportCommand.TryParse(["--environment", "Development", "seed-transport"], out var hostArgs));
        Assert.Equal(["--environment", "Development"], hostArgs);
        Assert.False(SeedTransportCommand.TryParse(["--urls", "http://x"], out var untouched));
        Assert.Equal(["--urls", "http://x"], untouched);
    }
}
