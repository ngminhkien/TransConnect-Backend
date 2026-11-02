using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransConnect.Modules.Notification.Options;
using TransConnect.Modules.Notification.Services;

namespace TransConnect.Modules.Notification.Workers;

public sealed class EmailDispatchWorker(IServiceScopeFactory scopes, IOptions<EmailOptions> options,
    ILogger<EmailDispatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.Worker.PollSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<EmailRetentionTask>().ExecuteAsync(stoppingToken);
                    await scope.ServiceProvider.GetRequiredService<EmailDispatcher>().ProcessBatchAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogError("Email worker pass failed with {ErrorType}.", ex.GetType().Name); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
