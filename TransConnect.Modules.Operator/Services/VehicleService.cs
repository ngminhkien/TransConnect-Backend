using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Transport;
using TransConnect.Modules.Operator.Dto.Requests;
using TransConnect.Modules.Operator.Dto.Responses;
using TransConnect.Modules.Operator.Entities;
using TransConnect.Modules.Operator.Persistence;

namespace TransConnect.Modules.Operator.Services;

public sealed class VehicleService(OperatorDbContext db, OperatorAccess access, ITransportObligations obligations) : IVehicleService
{
    public async Task<VehicleResponse> LinkAsync(Guid organizationId, LinkVehicleRequest request, CancellationToken ct)
    {
        // A platform reviewer records evidence; a partner cannot self-confirm operating rights.
        var admin = await access.AdminAsync(ct);
        var plate = new string(request.RegistrationPlate.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (plate.Length < 4) throw new ValidationAppException("Biển số không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(organizationId, ct);
        var org = await access.OrganizationAsync(organizationId, "manage", ct);
        if (org.ReviewStatus != "Approved") throw new ConflictException("Tổ chức chưa được phê duyệt.");
        var vehicle = await db.Vehicles.SingleOrDefaultAsync(x => x.RegistrationPlate == plate, ct);
        if (vehicle is null)
        {
            vehicle = new Vehicle { RegistrationPlate = plate, Description = request.Description };
            db.Vehicles.Add(vehicle);
        }
        else if (await db.OrganizationVehicles.AnyAsync(x => x.VehicleId == vehicle.Id && x.EndedAt == null, ct))
            throw new ConflictException("Xe vẫn còn liên kết với một tổ chức.");
        var link = new OrganizationVehicle
        {
            OrganizationId = organizationId, VehicleId = vehicle.Id, VerifiedBy = admin.AccountId,
            VerificationReference = request.VerificationReference
        };
        db.OrganizationVehicles.Add(link);
        access.Audit(organizationId, link.Id, "vehicle.link", plate + ": " + request.VerificationReference);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Unique plate and active VehicleId indexes arbitrate competing organizations.
            if (await db.OrganizationVehicles.AnyAsync(x => x.EndedAt == null
                && db.Vehicles.Any(v => v.Id == x.VehicleId && v.RegistrationPlate == plate), ct))
                throw new ConflictException("Xe đã được liên kết với một tổ chức.");
            throw;
        }
        await tx.CommitAsync(ct);
        return new(link.Id, vehicle.Id, plate, vehicle.Description, false, null);
    }

    public async Task<IReadOnlyList<VehicleResponse>> ListAsync(Guid organizationId, CancellationToken ct)
    {
        await access.OrganizationAsync(organizationId, "read", ct);
        return await (from link in db.OrganizationVehicles.AsNoTracking()
            join vehicle in db.Vehicles.AsNoTracking() on link.VehicleId equals vehicle.Id
            where link.OrganizationId == organizationId
            select new VehicleResponse(link.Id, vehicle.Id, vehicle.RegistrationPlate, vehicle.Description,
                link.IsSuspended, link.EndedAt)).ToListAsync(ct);
    }

    public async Task SuspendAsync(Guid organizationId, Guid linkId, SuspendVehicleRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(organizationId, ct);
        await access.OrganizationAsync(organizationId, "manage", ct);
        var link = await FindAsync(organizationId, linkId, ct);
        if (link.EndedAt != null) throw new ConflictException("Liên kết xe đã kết thúc.");
        link.IsSuspended = request.IsSuspended;
        access.Audit(organizationId, linkId, "vehicle.suspend", request.IsSuspended.ToString());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task EndAsync(Guid organizationId, Guid linkId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await access.LockAsync(organizationId, ct);
        await access.OrganizationAsync(organizationId, "manage", ct);
        var link = await FindAsync(organizationId, linkId, ct);
        if (await obligations.HasVehicleObligationsAsync(organizationId, link.VehicleId, ct))
            throw new ConflictException("Xe còn nghĩa vụ vận chuyển chưa giải quyết.");
        link.EndedAt ??= DateTime.UtcNow;
        link.IsSuspended = true;
        access.Audit(organizationId, linkId, "vehicle.end");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<OrganizationVehicle> FindAsync(Guid organizationId, Guid linkId, CancellationToken ct) =>
        await db.OrganizationVehicles.SingleOrDefaultAsync(x => x.OrganizationId == organizationId && x.Id == linkId, ct)
        ?? throw new NotFoundException("Liên kết xe", linkId);
}
