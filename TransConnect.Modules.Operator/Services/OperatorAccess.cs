using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Modules.Operator.Entities;
using TransConnect.Modules.Operator.Persistence;
using TransConnect.Common.Audit;

namespace TransConnect.Modules.Operator.Services;

public sealed class OperatorAccess(OperatorDbContext db, ICurrentUser current, IAccountDirectory accounts, IAuditContext? auditContext = null)
{
    public async Task<AccountSummary> AccountAsync(CancellationToken ct)
    {
        if (!current.IsAuthenticated || current.UserId is not { } id)
            throw new UnauthorizedAppException("Vui lòng đăng nhập.");
        return await accounts.FindActiveAsync(id, ct)
            ?? throw new UnauthorizedAppException("Tài khoản không còn khả dụng.");
    }

    public async Task<AccountSummary> OperatorAsync(CancellationToken ct)
    {
        var account = await AccountAsync(ct);
        if (account.Group != AccountGroups.Operator || !account.EmailVerified)
            throw new ForbiddenAppException("Cần tài khoản Operator đã xác thực email.");
        return account;
    }

    public async Task<AccountSummary> AdminAsync(CancellationToken ct)
    {
        var account = await AccountAsync(ct);
        if (account.Group != AccountGroups.InternalAdmin || !current.HasPermission("organizations.manage"))
            throw new ForbiddenAppException();
        return account;
    }

    public async Task<Organization> OrganizationAsync(Guid id, string capability, CancellationToken ct)
    {
        var account = await AccountAsync(ct);
        var org = await db.Organizations.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Tổ chức", id);
        if (account.Group == AccountGroups.InternalAdmin && current.HasPermission("organizations.manage"))
            return org;
        if (account.Group != AccountGroups.Operator || !account.EmailVerified || org.ReviewStatus != "Approved")
            throw new ForbiddenAppException();
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(
            x => x.OrganizationId == id && x.AccountId == account.AccountId && x.IsActive, ct);
        if (member is null || (capability == "owner" && member.Role != "OrganizationOwner")
            || (capability == "manage" && member.Role == "Dispatcher"))
            throw new ForbiddenAppException();
        return org;
    }

    // All organization mutations acquire this row before checking quotas/permissions.
    // Database lock, not an in-process semaphore, also covers multiple server instances.
    public Task<int> LockAsync(Guid id, CancellationToken ct) =>
        db.Organizations.Where(x => x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, x => x.UpdatedAt), ct);

    // Both Owner acceptance and Driver creation take this transaction-scoped lock first.
    // No Identity DbContext dependency; SQL Server coordinates across host instances.
    public async Task LockAccountAsync(Guid accountId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Account lock requires a transaction.");
        if (db.Database.IsSqlServer())
        {
            var resource = "operator-account:" + accountId.ToString("N");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive',
                    @LockOwner='Transaction', @LockTimeout=10000;
                IF @result < 0 THROW 51001, 'Could not acquire operator account lock.', 1;
                """, ct);
        }
        else if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // SQLite serializes writers, including when no row matches this update.
            await db.Members.IgnoreQueryFilters().Where(x => x.Id == Guid.Empty)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, x => x.IsActive), ct);
        }
        else throw new NotSupportedException("Unsupported account lock provider.");
    }

    public void Audit(Guid organizationId, Guid subjectId, string action, string details = "") =>
        db.AuditLogs.Add(new OperatorAuditLog
        {
            OrganizationId = organizationId == Guid.Empty ? null : organizationId,
            SubjectType = action switch
            {
                "member.invite" or "member.invitation.revoke" or "driver.email-invite" => "OrganizationInvitation",
                "member.accept" or "member.update" => "OrganizationMember",
                "driver.review" or "driver.profile-update" => "Driver",
                "driver.invite" or "driver.accept" or "driver.end" => "DriverEngagement",
                _ when action.StartsWith("organization.", StringComparison.Ordinal) => "Organization",
                _ when action.StartsWith("vehicle.", StringComparison.Ordinal) => "OrganizationVehicle",
                _ => "Unknown"
            },
            RequestId = auditContext?.RequestId,
            SubjectId = subjectId, ActorAccountId = current.UserId!.Value,
            Action = action, Details = details
        });
}
