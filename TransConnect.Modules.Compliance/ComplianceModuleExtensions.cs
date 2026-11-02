using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using TransConnect.Modules.Compliance.Options;
using TransConnect.Modules.Compliance.Persistence;
using TransConnect.Modules.Compliance.Services;
using TransConnect.Modules.Compliance.Storage;

namespace TransConnect.Modules.Compliance;

public static class ComplianceModuleExtensions
{
    public static IServiceCollection AddComplianceModule(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddDbContext<ComplianceDbContext>(o => o.UseSqlServer(configuration.GetConnectionString("Default")));
        services.AddOptions<ComplianceDemoOptions>().Bind(configuration.GetSection(ComplianceDemoOptions.SectionName))
            .Validate(o => !o.Enabled || environment.IsDevelopment(), "ComplianceDemo chỉ được bật trong Development.")
            .Validate(o => !o.Enabled || ValidEndpoint(o.Endpoint), "S3 endpoint phải là HTTPS hoặc HTTP loopback local, không chứa credentials/query.")
            .Validate(o => !o.Enabled || (!string.IsNullOrWhiteSpace(o.AccessKey) && !string.IsNullOrWhiteSpace(o.SecretKey)
                && !string.IsNullOrWhiteSpace(o.Region)), "Thiếu cấu hình S3 cho demo.")
            .Validate(o => !o.Enabled || System.Text.RegularExpressions.Regex.IsMatch(o.Bucket,
                "^transconnect-demo-[a-z0-9][a-z0-9-]{0,43}[a-z0-9]$"), "Demo phải dùng bucket riêng có tiền tố transconnect-demo-.")
            .ValidateOnStart();
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<ComplianceDemoOptions>>().Value;
            return new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), new AmazonS3Config
            {
                ServiceURL = o.Endpoint, AuthenticationRegion = o.Region, ForcePathStyle = o.ForcePathStyle,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                Timeout = TimeSpan.FromSeconds(30), MaxErrorRetry = 1
            });
        });
        services.AddScoped<IPrivateDocumentStorage, S3PrivateDocumentStorage>();
        services.AddScoped<DemoDocumentService>();
        services.AddScoped<IDemoDocumentService>(sp => sp.GetRequiredService<DemoDocumentService>());
        services.AddScoped<IProfileDocumentService, ProfileDocumentService>();
        services.AddScoped<TransConnect.Common.Maintenance.IMaintenanceTask, DemoAccountReferenceCheckTask>();
        return services;
    }

    private static bool ValidEndpoint(string endpoint) => Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && uri.AbsolutePath == "/" && (uri.Scheme == "https" || (uri.Scheme == "http" && uri.IsLoopback));
}
