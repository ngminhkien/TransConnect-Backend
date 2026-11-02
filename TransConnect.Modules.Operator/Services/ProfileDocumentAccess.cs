using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Transport;
using TransConnect.Modules.Operator.Persistence;

namespace TransConnect.Modules.Operator.Services;

public sealed class ProfileDocumentAccess(OperatorDbContext db, OperatorAccess access) : IProfileDocumentAccess
{
    public async Task<ProfileDocumentScope> AuthorizeAsync(string subjectType, Guid subjectId, bool write, CancellationToken ct)
    {
        var account = await access.OperatorAsync(ct);
        if (subjectType == "Driver")
        {
            var driver = await db.Drivers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == subjectId, ct)
                ?? throw new NotFoundException("Hồ sơ tài xế", subjectId);
            if (driver.AccountId == account.AccountId) return new(account.AccountId, null);
            if (write || !await db.Engagements.AnyAsync(e => e.DriverId == subjectId && e.AcceptedAt != null
                && e.EndedAt == null && e.RevokedAt == null
                && db.Organizations.Any(o => o.Id == e.OrganizationId && o.ReviewStatus == "Approved")
                && db.Members.Any(m => m.OrganizationId == e.OrganizationId && m.AccountId == account.AccountId
                    && m.IsActive && m.Role == "OrganizationOwner"), ct)) throw new ForbiddenAppException();
            return new(account.AccountId, null);
        }
        if (subjectType == "Vehicle")
        {
            var organizationId = await db.OrganizationVehicles.AsNoTracking().Where(x => x.VehicleId == subjectId && x.EndedAt == null
                && db.Vehicles.Any(v => v.Id == x.VehicleId)
                && db.Organizations.Any(o => o.Id == x.OrganizationId && o.ReviewStatus == "Approved")
                && db.Members.Any(m => m.OrganizationId == x.OrganizationId && m.AccountId == account.AccountId
                    && m.IsActive && m.Role == "OrganizationOwner"))
                .Select(x => (Guid?)x.OrganizationId).SingleOrDefaultAsync(ct);
            if (organizationId is null) throw new ForbiddenAppException();
            return new(account.AccountId, organizationId);
        }
        throw new ValidationAppException("Demo chỉ hỗ trợ hồ sơ Driver và Vehicle.");
    }
}
