using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransConnect.Common.Auth;
using TransConnect.Common.Maintenance;
using TransConnect.Modules.Operator.Persistence;

namespace TransConnect.Modules.Operator.Services;

public sealed class AccountReferenceCheckTask(OperatorDbContext db, IAccountReferenceDirectory accounts,
    IOptions<MaintenanceOptions> options, ILogger<AccountReferenceCheckTask> logger) : IMaintenanceTask
{
    public async Task ExecuteAsync(CancellationToken ct)
    {
        await CheckAsync("Member", db.Members.IgnoreQueryFilters().OrderBy(x => x.Id).Select(x => new Reference(x.Id, x.AccountId)), ct);
        await CheckAsync("OrganizationApplicant", db.Organizations.IgnoreQueryFilters().Where(x => x.CreatedByUserId != null)
            .OrderBy(x => x.Id).Select(x => new Reference(x.Id, x.CreatedByUserId!.Value)), ct);
        await CheckAsync("ProfileReviewer", db.ProfileReviews.OrderBy(x => x.Id).Select(x => new Reference(x.Id, x.ActorAccountId)), ct);
        await CheckAsync("Driver", db.Drivers.IgnoreQueryFilters().OrderBy(x => x.Id).Select(x => new Reference(x.Id, x.AccountId)), ct);
        await CheckAsync("OrganizationReviewer", db.Organizations.IgnoreQueryFilters().Where(x => x.ReviewedBy != null)
            .OrderBy(x => x.Id).Select(x => new Reference(x.Id, x.ReviewedBy!.Value)), ct);
        await CheckAsync("DriverReviewer", db.Drivers.IgnoreQueryFilters().Where(x => x.ReviewedBy != null)
            .OrderBy(x => x.Id).Select(x => new Reference(x.Id, x.ReviewedBy!.Value)), ct);
        await CheckAsync("InvitationAcceptor", db.Invitations.IgnoreQueryFilters().Where(x => x.AcceptedBy != null)
            .OrderBy(x => x.Id).Select(x => new Reference(x.Id, x.AcceptedBy!.Value)), ct);
        await CheckAsync("VehicleVerifier", db.OrganizationVehicles.IgnoreQueryFilters()
            .OrderBy(x => x.Id).Select(x => new Reference(x.Id, x.VerifiedBy)), ct);
        await CheckAsync("VehicleEndActor", db.OrganizationVehicles.IgnoreQueryFilters().Where(x => x.EndedBy != null)
            .OrderBy(x => x.Id).Select(x => new Reference(x.Id, x.EndedBy!.Value)), ct);
        await CheckAsync("AuditActor", db.AuditLogs.OrderBy(x => x.Id).Select(x => new Reference(x.Id, x.ActorAccountId)), ct);
    }

    private async Task CheckAsync(string type, IQueryable<Reference> query, CancellationToken ct)
    {
        for (var offset = 0; ; offset += options.Value.BatchSize)
        {
            var rows = await query.Skip(offset).Take(options.Value.BatchSize).ToListAsync(ct);
            if (rows.Count == 0) break;
            var missing = await accounts.FindMissingAsync(rows.Select(x => x.AccountId).Distinct().ToArray(), ct);
            foreach (var row in rows.Where(x => missing.Contains(x.AccountId)))
                logger.LogWarning("Orphan account reference {ReferenceType} {SubjectId} -> {AccountId}.", type, row.Id, row.AccountId);
        }
    }
    private sealed record Reference(Guid Id, Guid AccountId);
}
