using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransConnect.Common.Options;
using TransConnect.Data.Repositories;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Options;
using TransConnect.Modules.Identity.Persistence;
using TransConnect.Modules.Identity.Services;

namespace TransConnect.Modules.Identity;

public static class IdentityModuleExtensions
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<GoogleOptions>(configuration.GetSection(GoogleOptions.SectionName));
        services.Configure<VerificationOptions>(configuration.GetSection(VerificationOptions.SectionName));

        services.AddScoped<IRepository<User>, Repository<User, IdentityDbContext>>();
        services.AddScoped<IRepository<Role>, Repository<Role, IdentityDbContext>>();
        services.AddScoped<IRepository<Permission>, Repository<Permission, IdentityDbContext>>();
        services.AddScoped<IRepository<RefreshToken>, Repository<RefreshToken, IdentityDbContext>>();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IVerificationCodeService, VerificationCodeService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
