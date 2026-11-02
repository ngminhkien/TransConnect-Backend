using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Common.Transport;
using TransConnect.Modules.Operator.Persistence;

namespace TransConnect.Modules.Operator.Services;

public sealed class LocationOrganizationAccess(OperatorDbContext db, IAccountDirectory accounts)
    : ILocationOrganizationAccess
{
    public async Task RequireOwnerAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await accounts.FindActiveAsync(accountId, cancellationToken);
        if (account is null || account.Group != AccountGroups.Operator || !account.EmailVerified)
            throw new ForbiddenAppException("Cần tài khoản Owner đang hoạt động và đã xác thực email.");

        var isOwner = await db.Members.AsNoTracking().AnyAsync(member =>
            member.OrganizationId == organizationId && member.AccountId == accountId
            && member.IsActive && member.Role == "OrganizationOwner"
            && db.Organizations.Any(org => org.Id == organizationId && org.ReviewStatus == "Approved"),
            cancellationToken);
        if (!isOwner)
            throw new ForbiddenAppException("Chỉ Owner của tổ chức đã duyệt được đề xuất địa điểm.");
    }
}
