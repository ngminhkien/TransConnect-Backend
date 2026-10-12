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

        services.AddOptions<EmailOptions>().Bind(section)
            .Validate(o => string.Equals(o.Provider, EmailProviderNames.File, StringComparison.OrdinalIgnoreCase)
                || string.Equals(o.Provider, EmailProviderNames.Smtp, StringComparison.OrdinalIgnoreCase),
                "Email:Provider chỉ hỗ trợ File hoặc Smtp.")
            .Validate(o => o.Worker.PollSeconds > 0 && o.Worker.BatchSize > 0 && o.Worker.MaxAttempts > 0
                && o.Worker.StuckAfterMinutes > 0 && o.Worker.BackoffBaseSeconds > 0
                && o.Worker.BackoffMaxSeconds >= o.Worker.BackoffBaseSeconds,
                "Cấu hình Email:Worker không hợp lệ: các giá trị phải dương và backoff tối đa không nhỏ hơn backoff ban đầu.")
            .Validate(o => !string.Equals(o.Provider, EmailProviderNames.Smtp, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(o.Smtp.Host) && o.Smtp.Port is >= 1 and <= 65535
                    && !string.IsNullOrWhiteSpace(o.Smtp.User) && !string.IsNullOrWhiteSpace(o.Smtp.Password)
                    && System.Net.Mail.MailAddress.TryCreate(o.From.Address, out _)),
                "Provider Smtp cần Host, Port hợp lệ, User, Password và địa chỉ Email:From:Address hợp lệ.")
            .ValidateOnStart();

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
