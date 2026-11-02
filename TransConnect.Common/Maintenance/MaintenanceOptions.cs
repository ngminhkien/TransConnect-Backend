namespace TransConnect.Common.Maintenance;

public sealed class MaintenanceOptions
{
    public int PollSeconds { get; set; } = 3600;
    public int BatchSize { get; set; } = 200;
    public int AuditRetentionMonths { get; set; } = 6;
}
