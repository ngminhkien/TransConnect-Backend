using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Common.Audit;
using TransConnect.Common.Response;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Dto.Responses;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Services;

public sealed class AccountManagementService(IdentityDbContext db, ICurrentUser current, IAuditContext audit)
{
    private async Task RequireAdminAsync(CancellationToken ct)
    {
        if (!current.IsAuthenticated || !current.HasPermission("users.manage") ||
            !await db.Users.AnyAsync(x => x.Id == current.UserId && x.IsActive && x.AccountGroup == AccountGroups.InternalAdmin, ct))
            throw new ForbiddenAppException();
    }

    public async Task<PagedResult<AccountResponse>> ListAsync(int page, int pageSize, string? search, CancellationToken ct)
    {
        await RequireAdminAsync(ct);
        if (page < 1 || pageSize < 1 || pageSize > 100 || page > 100000)
            throw new ValidationAppException("Phân trang không hợp lệ.");
        if (search?.Length > 200) throw new ValidationAppException("Từ khóa tối đa 200 ký tự.");
        var query = db.Users.AsNoTracking();
        if (!current.HasPermission("admins.manage")) query = query.Where(x => x.AccountGroup != AccountGroups.InternalAdmin);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Email.Contains(search.Trim()) || x.FullName.Contains(search.Trim()));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Email).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(Response).ToList(), page, pageSize, total);
    }

    public async Task<PagedResult<AccountAuditResponse>> AuditAsync(Guid id, int pageNumber, int pageSize, CancellationToken ct)
    {
        await GetAsync(id, ct);
        if (pageNumber < 1 || pageNumber > 100000 || pageSize is < 1 or > 100)
            throw new ValidationAppException("Phân trang không hợp lệ.");
        var subjectId = id.ToString();
        var query = db.RbacAuditLogs.AsNoTracking().Where(x => x.SubjectType == "User" && x.SubjectId == subjectId && x.Action == "AccountStatus");
        var total = await query.CountAsync(ct);
        var logs = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var rows = logs.Select(x => {
            AccountStatusChange? change = null;
            try { change = JsonSerializer.Deserialize<AccountStatusChange>(x.ChangesJson); }
            catch (JsonException) { }
            return new AccountAuditResponse(x.Id, change?.Before, change?.After, change?.Reason, x.CreatedAt, x.RequestId);
        }).ToList();
        return new(rows, pageNumber, pageSize, total);
    }
    private sealed record AccountStatusChange(bool Before, bool After, string Reason);

    public async Task<AccountResponse> GetAsync(Guid id, CancellationToken ct)
    {
        await RequireAdminAsync(ct);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Tài khoản", id);
        CheckTarget(user);
        return Response(user);
    }

    public async Task<AccountResponse> SetStatusAsync(Guid id, SetAccountStatusRequest request, CancellationToken ct)
    {
        await RequireAdminAsync(ct);
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 1000)
            throw new ValidationAppException("Vui lòng nhập lý do tối đa 1000 ký tự.");
        if (id == current.UserId && !request.IsActive)
            throw new ConflictException("Không thể tự khóa tài khoản đang quản trị.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Include inactive accounts so unlocking is serialized with locking and refresh.
        await db.Users.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.UpdatedAt, x => x.UpdatedAt), ct);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Tài khoản", id);
        await db.Entry(user).ReloadAsync(ct);
        CheckTarget(user);
        var before = user.IsActive;
        user.IsActive = request.IsActive;
        if (!request.IsActive)
        {
            var now = DateTime.UtcNow;
            await db.RefreshTokens.Where(x => x.UserId == id && !x.IsRevoked).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.IsRevoked, true).SetProperty(x => x.RevokedAt, now)
                .SetProperty(x => x.RevokedReason, "AccountLocked").SetProperty(x => x.UpdatedAt, now), ct);
        }
        db.RbacAuditLogs.Add(new RbacAuditLog
        {
            ActorAccountId = current.UserId, RequestId = audit.RequestId, SubjectType = "User",
            SubjectId = id.ToString(), Action = "AccountStatus",
            ChangesJson = JsonSerializer.Serialize(new { Before = before, After = user.IsActive, Reason = request.Reason.Trim() })
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Response(user);
    }

    private void CheckTarget(User user)
    {
        if (user.AccountGroup == AccountGroups.InternalAdmin && !current.HasPermission("admins.manage"))
            throw new ForbiddenAppException("Cần quyền quản trị tài khoản nội bộ.");
    }

    private static AccountResponse Response(User x) =>
        new(x.Id, x.AccountGroup, x.Email, x.FullName, x.PhoneNumber, x.AvatarUrl, x.IsActive, x.EmailVerifiedAt != null);
}
