using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Auth;
using TransConnect.Common.Response;
using TransConnect.Modules.Transport.Dto;
using TransConnect.Modules.Transport.Services;

namespace TransConnect.Modules.Transport.Controllers;

public abstract class TransportControllerBase : ControllerBase
{
    protected IActionResult Result<T>(T data) => Ok(ApiResponse<T>.Success(data));
}

[ApiController, Authorize, Route("services")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ServiceTypesController(ServiceRegistrationService service, SeatLayoutService seats) : TransportControllerBase
{
    [HttpGet("types")]
    public async Task<IActionResult> Types(CancellationToken ct) => Result(await service.TypesAsync(ct));

    [HttpGet("seat-layout-templates")]
    public async Task<IActionResult> Templates(CancellationToken ct) => Result(await seats.TemplatesAsync(ct));
}

[ApiController, Authorize, Route("organizations/{organizationId:guid}/services")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrganizationServicesController(ServiceRegistrationService service) : TransportControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken ct) => Result(await service.ForOrganizationAsync(organizationId, ct));

    [HttpPost]
    public async Task<IActionResult> Submit(Guid organizationId, SubmitRegistrationRequest request, CancellationToken ct)
        => Result(await service.SubmitAsync(organizationId, request, ct));

    [HttpPost("{id:guid}/resubmit")]
    public async Task<IActionResult> Resubmit(Guid organizationId, Guid id, ResubmitRegistrationRequest request, CancellationToken ct)
        => Result(await service.ResubmitAsync(organizationId, id, request, ct));

    [HttpPost("{id:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid organizationId, Guid id, NoteRequest request, CancellationToken ct)
        => Result(await service.WithdrawAsync(organizationId, id, request, ct));
}

[ApiController, Authorize, RequirePermission(TransportAccess.ReviewPermission), Route("services/admin")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ServicesAdminController(ServiceRegistrationService service, RouteService routes) : TransportControllerBase
{
    [HttpGet("registrations")]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] string? status = null, [FromQuery] string? serviceTypeCode = null,
        [FromQuery] string? search = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
        => Result(await service.AdminListAsync(status, serviceTypeCode, search, pageNumber, pageSize, ct));

    [HttpGet("registrations/{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct) => Result(await service.AdminDetailAsync(id, ct));

    [HttpPost("registrations/{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, ReviewRegistrationRequest request, CancellationToken ct)
        => Result(await service.ReviewAsync(id, request, ct));

    [HttpPost("registrations/{id:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid id, NoteRequest request, CancellationToken ct) => Result(await service.SuspendAsync(id, request, ct));

    [HttpPost("registrations/{id:guid}/reinstate")]
    public async Task<IActionResult> Reinstate(Guid id, NoteRequest request, CancellationToken ct) => Result(await service.ReinstateAsync(id, request, ct));

    [HttpGet("organizations/{organizationId:guid}/routes")]
    public async Task<IActionResult> Routes(Guid organizationId, CancellationToken ct) => Result(await routes.AdminListAsync(organizationId, ct));

    [HttpPost("routes/{id:guid}/deactivate")]
    public async Task<IActionResult> DeactivateRoute(Guid id, NoteRequest request, CancellationToken ct)
        => Result(await routes.AdminDeactivateAsync(id, request, ct));
}

[ApiController, Authorize, Route("organizations/{organizationId:guid}/routes")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RoutesController(RouteService service) : TransportControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken ct) => Result(await service.ListAsync(organizationId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid organizationId, Guid id, CancellationToken ct) => Result(await service.GetAsync(organizationId, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(Guid organizationId, SaveRouteRequest request, CancellationToken ct)
        => Result(await service.CreateAsync(organizationId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid organizationId, Guid id, SaveRouteRequest request, CancellationToken ct)
        => Result(await service.UpdateAsync(organizationId, id, request, ct));

    [HttpPost("{id:guid}/revisions")]
    public async Task<IActionResult> CreateDraft(Guid organizationId, Guid id, CancellationToken ct)
        => Result(await service.CreateDraftAsync(organizationId, id, ct));

    [HttpPut("{id:guid}/revisions/{revisionId:guid}")]
    public async Task<IActionResult> SaveDraft(Guid organizationId, Guid id, Guid revisionId, SaveDraftRequest request, CancellationToken ct)
        => Result(await service.SaveDraftAsync(organizationId, id, revisionId, request, ct));

    [HttpDelete("{id:guid}/revisions/{revisionId:guid}")]
    public async Task<IActionResult> DeleteDraft(Guid organizationId, Guid id, Guid revisionId, CancellationToken ct)
        => Result(await service.DeleteDraftAsync(organizationId, id, revisionId, ct));

    [HttpPost("{id:guid}/revisions/{revisionId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid organizationId, Guid id, Guid revisionId, VersionRequest request, CancellationToken ct)
        => Result(await service.PublishAsync(organizationId, id, revisionId, request, ct));

    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid organizationId, Guid id, NoteRequest request, CancellationToken ct)
        => Result(await service.DeactivateAsync(organizationId, id, request, ct));

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid organizationId, Guid id, NoteRequest request, CancellationToken ct)
        => Result(await service.ActivateAsync(organizationId, id, request, ct));
}

[ApiController, Authorize, Route("organizations/{organizationId:guid}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SeatLayoutsController(SeatLayoutService service) : TransportControllerBase
{
    [HttpGet("seat-layouts")]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken ct) => Result(await service.ListAsync(organizationId, ct));

    [HttpGet("seat-layouts/{id:guid}")]
    public async Task<IActionResult> Get(Guid organizationId, Guid id, CancellationToken ct) => Result(await service.GetAsync(organizationId, id, ct));

    [HttpPost("seat-layouts")]
    public async Task<IActionResult> Create(Guid organizationId, SaveSeatLayoutRequest request, CancellationToken ct)
        => Result(await service.CreateAsync(organizationId, request, ct));

    [HttpPut("seat-layouts/{id:guid}")]
    public async Task<IActionResult> Update(Guid organizationId, Guid id, SaveSeatLayoutRequest request, CancellationToken ct)
        => Result(await service.UpdateAsync(organizationId, id, request, ct));

    [HttpDelete("seat-layouts/{id:guid}")]
    public async Task<IActionResult> Archive(Guid organizationId, Guid id, [FromQuery] int version, CancellationToken ct)
    {
        await service.ArchiveAsync(organizationId, id, version, ct);
        return Result(new { id });
    }

    [HttpGet("vehicle-seat-layouts")]
    public async Task<IActionResult> VehicleLayouts(Guid organizationId, CancellationToken ct)
        => Result(await service.VehicleLayoutsAsync(organizationId, ct));

    [HttpGet("vehicles/{vehicleId:guid}/seat-layout")]
    public async Task<IActionResult> VehicleLayout(Guid organizationId, Guid vehicleId, CancellationToken ct)
        => Result(await service.VehicleLayoutAsync(organizationId, vehicleId, ct));

    [HttpPut("vehicles/{vehicleId:guid}/seat-layout")]
    public async Task<IActionResult> Attach(Guid organizationId, Guid vehicleId, AttachSeatLayoutRequest request, CancellationToken ct)
        => Result(await service.AttachAsync(organizationId, vehicleId, request, ct));
}

[ApiController, Authorize, Route("organizations/{organizationId:guid}/schedules")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SchedulesController(ScheduleService service) : TransportControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid organizationId, CancellationToken ct, [FromQuery] Guid? routeId = null)
        => Result(await service.ListAsync(organizationId, routeId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid organizationId, Guid id, CancellationToken ct) => Result(await service.GetAsync(organizationId, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(Guid organizationId, SaveScheduleRequest request, CancellationToken ct)
        => Result(await service.CreateAsync(organizationId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid organizationId, Guid id, SaveScheduleRequest request, CancellationToken ct)
        => Result(await service.UpdateAsync(organizationId, id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid organizationId, Guid id, [FromQuery] int version, CancellationToken ct)
    {
        await service.DeleteDraftAsync(organizationId, id, version, ct);
        return Result(new { id });
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid organizationId, Guid id, ScheduleStatusRequest request, CancellationToken ct)
        => Result(await service.ActivateAsync(organizationId, id, request, ct));

    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid organizationId, Guid id, ScheduleStatusRequest request, CancellationToken ct)
        => Result(await service.PauseAsync(organizationId, id, request, ct));

    [HttpPost("{id:guid}/end")]
    public async Task<IActionResult> End(Guid organizationId, Guid id, ScheduleStatusRequest request, CancellationToken ct)
        => Result(await service.EndAsync(organizationId, id, request, ct));

    [HttpPost("{id:guid}/replace")]
    public async Task<IActionResult> Replace(Guid organizationId, Guid id, ReplaceScheduleRequest request, CancellationToken ct)
        => Result(await service.ReplaceAsync(organizationId, id, request, ct));
}

[ApiController, Authorize, Route("organizations/{organizationId:guid}/routes/{routeId:guid}/fares")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class FaresController(FareService service) : TransportControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid organizationId, Guid routeId, CancellationToken ct)
        => Result(await service.ListAsync(organizationId, routeId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid organizationId, Guid routeId, Guid id, CancellationToken ct)
        => Result(await service.GetAsync(organizationId, routeId, id, ct));

    [HttpPost]
    public async Task<IActionResult> Create(Guid organizationId, Guid routeId, CreateFareRequest request, CancellationToken ct)
        => Result(await service.CreateAsync(organizationId, routeId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Save(Guid organizationId, Guid routeId, Guid id, SaveFareRequest request, CancellationToken ct)
        => Result(await service.SaveAsync(organizationId, routeId, id, request, ct));

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid organizationId, Guid routeId, Guid id, VersionRequest request, CancellationToken ct)
        => Result(await service.PublishAsync(organizationId, routeId, id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid organizationId, Guid routeId, Guid id, [FromQuery] int version, CancellationToken ct)
    {
        await service.DeleteDraftAsync(organizationId, routeId, id, version, ct);
        return Result(new { id });
    }
}
