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

        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Key) && System.Text.Encoding.UTF8.GetByteCount(o.Key) >= 32,
                "Jwt:Key phải có ít nhất 32 byte cho HS256.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Thiếu Jwt:Issuer.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Thiếu Jwt:Audience.")
            .Validate(o => o.AccessTokenMinutes > 0 && o.RefreshTokenDays > 0, "Thời hạn JWT/refresh token phải lớn hơn 0.")
            .ValidateOnStart();
        services.Configure<GoogleOptions>(configuration.GetSection(GoogleOptions.SectionName));
        services.AddOptions<VerificationOptions>().Bind(configuration.GetSection(VerificationOptions.SectionName))
            .Validate(o => o.CodeLength is >= 1 and <= 9, "Verification:CodeLength phải từ 1 đến 9 để sinh mã bằng Int32.")
            .Validate(o => o.ExpiryMinutes > 0 && o.MaxAttempts > 0 && o.MaxRequestsPerHour > 0,
                "Thời hạn và giới hạn OTP phải lớn hơn 0.")
            .ValidateOnStart();

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
