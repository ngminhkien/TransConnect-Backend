namespace TransConnect.Common.Maintenance;

// Each implementation owns its module's DbContext. The host only schedules the contract.
public interface IMaintenanceTask
{
    Task ExecuteAsync(CancellationToken ct);
}
