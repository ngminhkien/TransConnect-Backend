using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Transport;
using TransConnect.Modules.Transport.Entities;
using TransConnect.Modules.Transport.Persistence;

namespace TransConnect.Modules.Transport.Services;

internal static class ScheduleRules
{
    public static async Task<ServiceRegistration> ApprovedCoachAsync(TransportDbContext db, Guid organizationId, CancellationToken ct)
        => await db.Registrations.AsNoTracking().SingleOrDefaultAsync(x => x.OrganizationId == organizationId
               && x.ServiceTypeCode == ServiceTypeCodes.FixedRouteCoach && x.Status == RegistrationStatuses.Approved, ct)
           ?? throw new ConflictException("Tổ chức chưa có dịch vụ xe khách tuyến cố định được duyệt.");

    public static async Task<bool> RegistrationApprovedAsync(TransportDbContext db, Guid registrationId, CancellationToken ct)
        => await db.Registrations.AnyAsync(x => x.Id == registrationId && x.Status == RegistrationStatuses.Approved, ct);

    public static async Task PauseForRegistrationAsync(TransportDbContext db, Guid registrationId, string reason, CancellationToken ct)
    {
        var routeIds = await db.Routes.Where(x => x.ServiceRegistrationId == registrationId).Select(x => x.Id).ToListAsync(ct);
        await PauseAsync(db, routeIds, reason, ct);
    }

    public static Task PauseForRouteAsync(TransportDbContext db, Guid routeId, string reason, CancellationToken ct)
        => PauseAsync(db, [routeId], reason, ct);

    private static async Task PauseAsync(TransportDbContext db, IReadOnlyCollection<Guid> routeIds, string reason, CancellationToken ct)
    {
        if (routeIds.Count == 0) return;
        var schedules = await db.Schedules.Where(x => routeIds.Contains(x.RouteId) && x.Status == ScheduleStatuses.Active).ToListAsync(ct);
        foreach (var schedule in schedules)
        {
            schedule.Status = ScheduleStatuses.Paused;
            schedule.StatusReason = reason.Length > 1000 ? reason[..1000] : reason;
            schedule.Version++;
        }
    }
}
