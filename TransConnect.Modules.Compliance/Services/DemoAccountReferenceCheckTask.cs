using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransConnect.Common.Auth;
using TransConnect.Common.Maintenance;
using TransConnect.Modules.Compliance.Options;
using TransConnect.Modules.Compliance.Persistence;

namespace TransConnect.Modules.Compliance.Services;

public sealed class DemoAccountReferenceCheckTask(ComplianceDbContext db, IAccountReferenceDirectory accounts,
    IOptions<ComplianceDemoOptions> demo, IOptions<MaintenanceOptions> options,
    ILogger<DemoAccountReferenceCheckTask> logger) : IMaintenanceTask
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        if (!demo.Value.Enabled) return; // Disabled demo has no guaranteed migrated table.
        for (var offset = 0; ; offset += options.Value.BatchSize)
        {
            var rows = await db.DemoDocuments.IgnoreQueryFilters().AsNoTracking().OrderBy(x => x.Id)
                .Skip(offset).Take(options.Value.BatchSize).Select(x => new { x.Id, x.AccountId }).ToListAsync(ct);
            if (rows.Count == 0) break;
            var missing = await accounts.FindMissingAsync(rows.Select(x => x.AccountId).Distinct().ToArray(), ct);
            foreach (var row in rows.Where(x => missing.Contains(x.AccountId)))
                logger.LogWarning("Orphan demo document {DocumentId} -> {AccountId}.", row.Id, row.AccountId);
        }
    }
}
