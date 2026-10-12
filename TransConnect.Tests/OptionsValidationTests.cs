using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TransConnect.Modules.Identity;
using TransConnect.Modules.Notification;
using Xunit;

namespace TransConnect.Tests;

public class OptionsValidationTests
{
    [Theory]
    [InlineData("Jwt:Key", "short")]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", "")]
    [InlineData("Jwt:AccessTokenMinutes", "0")]
    [InlineData("Jwt:RefreshTokenDays", "0")]
    [InlineData("Verification:CodeLength", "10")]
    [InlineData("Verification:MaxAttempts", "0")]
    [InlineData("Verification:ExpiryMinutes", "0")]
    [InlineData("Verification:MaxRequestsPerHour", "0")]
    [InlineData("Email:Provider", "typo")]
    [InlineData("Email:Worker:PollSeconds", "0")]
    [InlineData("Email:Worker:BatchSize", "0")]
    [InlineData("Email:Worker:MaxAttempts", "0")]
    [InlineData("Email:Worker:BackoffMaxSeconds", "1")]
    [InlineData("Email:Worker:StuckAfterMinutes", "0")]
    [InlineData("Email:Provider", "Smtp")]
    public void TC_CORE_001_Invalid_configuration_fails_startup(string key, string value)
    {
        using var services = CreateServices(key, value);
        Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public void TC_CORE_002_File_provider_and_disabled_google_need_no_external_credentials()
    {
        using var services = CreateServices("Email:Provider", "File");
        services.GetRequiredService<IStartupValidator>().Validate();
    }

    private static ServiceProvider CreateServices(string key, string value)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-only-key-0123456789-abcdefghijklmnopqrstuvwxyz",
            ["Jwt:Issuer"] = "Tests",
            ["Jwt:Audience"] = "Tests"
        };
        values[key] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityModule(configuration);
        services.AddNotificationModule(configuration);
        return services.BuildServiceProvider();
    }
}
