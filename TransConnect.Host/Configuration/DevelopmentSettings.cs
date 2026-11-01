using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace TransConnect.Host.Configuration;

/// <summary>Development application settings come only from the two visible JSON files.</summary>
public static class DevelopmentSettings
{
    public static void Apply(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment()) return;

        // Keep the launch profile's transport settings (e.g. port 5267), not application overrides.
        var hostingKeys = new[] { "urls", "http_ports", "https_ports" };
        var hosting = hostingKeys.Where(key => builder.Configuration[key] is not null)
            .ToDictionary(key => key, key => builder.Configuration[key]);

        builder.Configuration.Sources.Clear();
        builder.Configuration.SetBasePath(builder.Environment.ContentRootPath)
            .AddInMemoryCollection(hosting)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: false, reloadOnChange: true);
    }
}
