using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransConnect.Common.Maintenance;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Services;

public sealed class IdentityAuditRetentionTask(IdentityDbContext db, IOptions<MaintenanceOptions> options,
    TimeProvider time) : IMaintenanceTask
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        var cutoff = time.GetUtcNow().UtcDateTime.AddMonths(-options.Value.AuditRetentionMonths);
        var ids = await db.RbacAuditLogs.Where(x => x.CreatedAt < cutoff).OrderBy(x => x.CreatedAt)
            .Take(options.Value.BatchSize).Select(x => x.Id).ToListAsync(ct);
        await db.RbacAuditLogs.Where(x => ids.Contains(x.Id) && x.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
    }
}
