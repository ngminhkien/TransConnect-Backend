using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Common.Transport;
using TransConnect.Modules.Transport.Persistence;

namespace TransConnect.Modules.Transport.Services;

public sealed class TransportAccess(ICurrentUser current, IAccountDirectory accounts, ITransportOrganizationAccess organizations,
    TimeProvider time)
{
    public const string ReviewPermission = "services.review";
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public ITransportOrganizationAccess Organizations => organizations;

    public DateOnly Today => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime + VietnamOffset);

    public async Task<AccountSummary> ActorAsync(CancellationToken ct)
    {
        var id = current.UserId ?? throw new UnauthorizedAppException("Vui lòng đăng nhập.");
        return await accounts.FindActiveAsync(id, ct) ?? throw new UnauthorizedAppException("Tài khoản không còn khả dụng.");
    }

    public async Task<Guid> AdminAsync(CancellationToken ct)
    {
        var account = await ActorAsync(ct);
        if (account.Group != AccountGroups.InternalAdmin || !current.HasPermission(ReviewPermission))
            throw new ForbiddenAppException("Bạn chưa có quyền duyệt dịch vụ.");
        return account.AccountId;
    }

    public async Task<TransportMembership> MemberAsync(Guid organizationId, CancellationToken ct)
    {
        var account = await ActorAsync(ct);
        return await organizations.RequireMemberAsync(organizationId, account.AccountId, ct);
    }

    public async Task<TransportMembership> OwnerAsync(Guid organizationId, CancellationToken ct)
    {
        var member = await MemberAsync(organizationId, ct);
        if (!member.IsOwner) throw new ForbiddenAppException("Chỉ Owner của tổ chức được thực hiện thao tác này.");
        return member;
    }

    /// <summary>Thành viên tổ chức hoặc Admin có quyền duyệt dịch vụ (chỉ đọc). Trả về id người thao tác.</summary>
    public async Task<Guid> ReaderAsync(Guid organizationId, CancellationToken ct)
    {
        var account = await ActorAsync(ct);
        if (account.Group == AccountGroups.InternalAdmin)
        {
            if (!current.HasPermission(ReviewPermission)) throw new ForbiddenAppException();
            return account.AccountId;
        }
        await organizations.RequireMemberAsync(organizationId, account.AccountId, ct);
        return account.AccountId;
    }
}

internal static class Guard
{
    public static void Check(bool valid, string field, string message)
    {
        if (!valid) throw new ValidationAppException(new Dictionary<string, string[]> { [field] = [message] });
    }

    public static string Required(string? value, int max, string field)
    {
        Check(!string.IsNullOrWhiteSpace(value) && value.Trim().Length <= max, field, $"Trường bắt buộc, tối đa {max} ký tự.");
        return value!.Trim();
    }

    public static string? Optional(string? value, int max, string field)
    {
        Check(value is null || value.Trim().Length <= max, field, $"Tối đa {max} ký tự.");
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static void Version(int expected, int actual)
    {
        if (expected != actual) throw new ConflictException("Dữ liệu đã thay đổi. Vui lòng tải lại và thử lại.");
    }

    public static async Task SaveAsync(TransportDbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ConflictException("Dữ liệu đã được người khác cập nhật. Vui lòng tải lại."); }
        catch (DbUpdateException) { throw new ConflictException("Dữ liệu xung đột với bản hiện có. Vui lòng kiểm tra và tải lại."); }
    }
}
