using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransConnect.Common.Maintenance;
using TransConnect.Modules.Operator.Persistence;

namespace TransConnect.Modules.Operator.Services;

public sealed class OperatorAuditRetentionTask(OperatorDbContext db, IOptions<MaintenanceOptions> options,
    TimeProvider time) : IMaintenanceTask
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var cutoff = time.GetUtcNow().UtcDateTime.AddMonths(-options.Value.AuditRetentionMonths);
        var ids = await db.AuditLogs.Where(x => x.CreatedAt < cutoff).OrderBy(x => x.CreatedAt)
            .Take(options.Value.BatchSize).Select(x => x.Id).ToListAsync(ct);
        await db.AuditLogs.Where(x => ids.Contains(x.Id) && x.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
    }
}
