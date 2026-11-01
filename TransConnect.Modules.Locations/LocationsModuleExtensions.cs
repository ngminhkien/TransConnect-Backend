using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TransConnect.Modules.Locations.Persistence;
using TransConnect.Modules.Locations.Services;
namespace TransConnect.Modules.Locations;

public static class LocationsModuleExtensions
{
    public static IServiceCollection AddLocationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LocationsDbContext>(o => o.UseSqlServer(configuration.GetConnectionString("Default")));
        services.AddScoped<ILocationsService, LocationsService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<PlaceSearchOptions>().Bind(configuration.GetSection(PlaceSearchOptions.Section))
            .Validate(o => Uri.TryCreate(o.Endpoint, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
                string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment),
                "Place search endpoint must use HTTPS without credentials, query or fragment.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.UserAgent), "Place search must identify VieGo.")
            .ValidateOnStart();
        services.AddHttpClient("VieGoPlaceSearch", (sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
            client.MaxResponseContentBufferSize = 512 * 1024;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlaceSearchOptions>>().Value.UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).RemoveAllLoggers();
        services.AddSingleton<PlaceSearchService>();
        return services;
    }
}
