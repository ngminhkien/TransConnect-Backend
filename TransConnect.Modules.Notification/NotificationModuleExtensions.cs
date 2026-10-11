using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TransConnect.Common.Notifications;
using TransConnect.Modules.Notification.Options;
using TransConnect.Modules.Notification.Persistence;
using TransConnect.Modules.Notification.Providers;
using TransConnect.Modules.Notification.Services;
using TransConnect.Modules.Notification.Templates;
using TransConnect.Modules.Notification.Workers;

namespace TransConnect.Modules.Notification;

public static class NotificationModuleExtensions
{
    public static IServiceCollection AddNotificationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(EmailOptions.SectionName);

        services.AddDbContext<NotificationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.Configure<EmailOptions>(section);

        // Chọn đường gửi qua config thay vì #if DEBUG, để đổi được mà không phải build lại.
        var providerName = section[nameof(EmailOptions.Provider)] ?? EmailProviderNames.File;

        if (string.Equals(providerName, EmailProviderNames.Smtp, StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailProvider, SmtpEmailProvider>();
        }
        else
        {
            services.AddScoped<IEmailProvider, FileEmailProvider>();
        }

        // Singleton: chỉ đọc config và cache nội dung template, không giữ state theo request.
        services.AddSingleton<ITemplateRenderer, EmbeddedTemplateRenderer>();

        services.AddScoped<IEmailQueue, EmailQueue>();

        services.AddHostedService<EmailDispatchWorker>();

        return services;
    }
}
