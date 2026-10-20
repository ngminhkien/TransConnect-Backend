using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TransConnect.Common.Maintenance;

public sealed class MaintenanceWorker(IServiceScopeFactory scopes, IOptions<MaintenanceOptions> options,
    ILogger<MaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                // Separate scopes prevent one failed task's tracked state leaking into another task.
                using var catalog = scopes.CreateScope();
                var types = catalog.ServiceProvider.GetServices<IMaintenanceTask>().Select(x => x.GetType()).ToArray();
                foreach (var type in types)
                {
                    using var scope = scopes.CreateScope();
                    try
                    {
                        var task = scope.ServiceProvider.GetServices<IMaintenanceTask>().Single(x => x.GetType() == type);
                        await task.ExecuteAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex) { logger.LogError(ex, "Maintenance task {TaskType} failed.", type.Name); }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
