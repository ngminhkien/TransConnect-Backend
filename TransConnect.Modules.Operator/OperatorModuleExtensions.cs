using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TransConnect.Common.Transport;
using TransConnect.Modules.Operator.Options;
using TransConnect.Modules.Operator.Persistence;
using TransConnect.Modules.Operator.Services;

namespace TransConnect.Modules.Operator;

public static class OperatorModuleExtensions
{
    public static IServiceCollection AddOperatorModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OperatorDbContext>(o => o.UseSqlServer(configuration.GetConnectionString("Default")));
        services.AddOptions<OperatorOptions>().Bind(configuration.GetSection(OperatorOptions.SectionName))
            .Validate(o => o.DefaultEmployeeLimit > 0 && o.InvitationHours > 0, "Hạn mức và thời hạn lời mời phải lớn hơn 0.")
            .Validate(o => o.DefaultDriverLimit is null or > 0, "Hạn mức tài xế phải lớn hơn 0 hoặc để trống.")
            .ValidateOnStart();
        services.AddScoped<OperatorAccess>();
        services.AddScoped<IOrganizationService, OrganizationService>();
        services.AddScoped<IDriverService, DriverService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.TryAddScoped<ITransportObligations, FoundationTransportObligations>();
        return services;
    }
}
