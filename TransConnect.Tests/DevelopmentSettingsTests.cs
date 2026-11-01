using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using TransConnect.Host.Configuration;
using Xunit;

namespace TransConnect.Tests;

public sealed class DevelopmentSettingsTests
{
    [Fact]
    public void Development_uses_JSON_only_and_preserves_the_launch_profile_URL()
    {
        using var fixture = new SettingsFixture();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development", ContentRootPath = fixture.Directory,
            Args = ["--Email:Provider=File", "--ConnectionStrings:Default=unexpected-cli-database"]
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "unexpected-secret-or-environment-database",
            ["Email:Provider"] = "File", ["HiddenApplicationSetting"] = "unexpected",
            ["urls"] = "http://localhost:5267"
        });

        DevelopmentSettings.Apply(builder);

        Assert.Equal("visible-development-database", builder.Configuration.GetConnectionString("Default"));
        Assert.Equal("Smtp", builder.Configuration["Email:Provider"]);
        Assert.Equal("base-file-value", builder.Configuration["BaseOnlySetting"]);
        Assert.Null(builder.Configuration["HiddenApplicationSetting"]);
        Assert.Equal("http://localhost:5267", builder.Configuration["urls"]);
        Assert.DoesNotContain(builder.Configuration.Sources, source => source.GetType().Name.Contains("EnvironmentVariables")
            || source.GetType().Name.Contains("CommandLine"));
    }

    [Fact]
    public void Testing_keeps_the_isolated_test_host_configuration()
    {
        using var fixture = new SettingsFixture();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing", ContentRootPath = fixture.Directory
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = "isolated-test-key" });
        DevelopmentSettings.Apply(builder);
        Assert.Equal("isolated-test-key", builder.Configuration["Jwt:Key"]);
    }

    [Fact]
    public void Missing_Development_file_fails_instead_of_using_a_hidden_source()
    {
        using var fixture = new SettingsFixture();
        File.Delete(Path.Combine(fixture.Directory, "appsettings.Development.json"));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development", ContentRootPath = fixture.Directory
        });
        Assert.Throws<FileNotFoundException>(() => DevelopmentSettings.Apply(builder));
    }

    private sealed class SettingsFixture : IDisposable
    {
        private readonly string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TransConnect-configuration-tests"));
        public string Directory { get; }
        public SettingsFixture()
        {
            Directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(Directory);
            File.WriteAllText(Path.Combine(Directory, "appsettings.json"), JsonSerializer.Serialize(new
            {
                ConnectionStrings = new { Default = "visible-base-database" }, BaseOnlySetting = "base-file-value"
            }));
            File.WriteAllText(Path.Combine(Directory, "appsettings.Development.json"), JsonSerializer.Serialize(new
            {
                ConnectionStrings = new { Default = "visible-development-database" }, Email = new { Provider = "Smtp" }
            }));
        }
        public void Dispose()
        {
            var path = Path.GetFullPath(Directory);
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Configuration fixture escaped its temporary root.");
            System.IO.Directory.Delete(path, recursive: true);
        }
    }
}
