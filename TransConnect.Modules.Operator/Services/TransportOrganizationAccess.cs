using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Common.Transport;
using TransConnect.Modules.Operator.Persistence;

namespace TransConnect.Modules.Operator.Services;

public sealed class TransportOrganizationAccess(OperatorDbContext db, IAccountDirectory accounts) : ITransportOrganizationAccess
{
    public async Task<TransportMembership> RequireMemberAsync(Guid organizationId, Guid accountId, CancellationToken ct = default)
    {
        var account = await accounts.FindActiveAsync(accountId, ct);
        if (account is null || account.Group != AccountGroups.Operator || !account.EmailVerified)
            throw new ForbiddenAppException("Cần tài khoản Operator đang hoạt động và đã xác thực email.");
        var member = await db.Members.AsNoTracking().SingleOrDefaultAsync(m =>
            m.OrganizationId == organizationId && m.AccountId == accountId && m.IsActive
            && db.Organizations.Any(o => o.Id == organizationId && o.ReviewStatus == "Approved"), ct);
        // Not-a-member and unknown organization look the same so other organizations' ids do not leak.
        return member is null
            ? throw new NotFoundException("Không tìm thấy tổ chức.")
            : new TransportMembership(organizationId, accountId, member.Role);
    }

    public async Task<TransportOrganization?> FindAsync(Guid organizationId, CancellationToken ct = default)
        => (await FindManyAsync([organizationId], ct)).GetValueOrDefault(organizationId);

    public async Task<IReadOnlyDictionary<Guid, TransportOrganization>> FindManyAsync(IReadOnlyCollection<Guid> organizationIds, CancellationToken ct = default)
    {
        if (organizationIds.Count == 0) return new Dictionary<Guid, TransportOrganization>();
        var rows = await db.Organizations.AsNoTracking().Where(x => organizationIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.RegistrationNumber, x.ReviewStatus, x.LegalEntityType, x.RequestedServiceTypes })
            .ToListAsync(ct);
        return rows.ToDictionary(x => x.Id, x => new TransportOrganization(x.Id, x.Name, x.RegistrationNumber, x.ReviewStatus,
            x.LegalEntityType, OrganizationService.SplitServiceTypes(x.RequestedServiceTypes)));
    }

    public async Task<TransportVehicle?> FindManagedVehicleAsync(Guid organizationId, Guid vehicleId, CancellationToken ct = default)
    {
        var link = await db.OrganizationVehicles.AsNoTracking()
            .AnyAsync(x => x.OrganizationId == organizationId && x.VehicleId == vehicleId && x.EndedAt == null, ct);
        if (!link) return null;
        var vehicle = await db.Vehicles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == vehicleId, ct);
        return vehicle is null ? null
            : new TransportVehicle(vehicle.Id, organizationId, vehicle.RegistrationPlate, vehicle.VehicleKind, vehicle.SeatCount);
    }
}
