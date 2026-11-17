using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TransConnect.Common.Transport;
using TransConnect.Modules.Transport.Persistence;
using TransConnect.Modules.Transport.Services;

namespace TransConnect.Modules.Transport;

public static class TransportModuleExtensions
{
    public const string ApplyMigrationsSetting = "Transport:ApplyMigrationsOnStartup";

    public static IServiceCollection AddTransportModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TransportDbContext>(o => o.UseSqlServer(configuration.GetConnectionString("Default")));
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<TransportAccess>();
        services.AddScoped<ServiceRegistrationService>();
        services.AddScoped<RouteService>();
        services.AddScoped<SeatLayoutService>();
        services.AddScoped<ScheduleService>();
        services.AddScoped<FareService>();
        // Operator đăng ký bản no-op bằng TryAdd; Transport thay bằng bản thật.
        services.Replace(ServiceDescriptor.Scoped<IServiceRegistrationIntake>(sp => sp.GetRequiredService<ServiceRegistrationService>()));
        return services;
    }
}
