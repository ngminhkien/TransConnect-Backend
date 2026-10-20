using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Response;
using TransConnect.Modules.Operator.Dto.Requests;
using TransConnect.Modules.Operator.Dto.Responses;
using TransConnect.Modules.Operator.Services;

namespace TransConnect.Modules.Operator.Controllers;

[ApiController]
[Authorize]
[Route("organizations/{organizationId:guid}/vehicles")]
public sealed class VehiclesController(IVehicleService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<VehicleResponse>>> Link(Guid organizationId, LinkVehicleRequest request, CancellationToken ct)
    {
        var result = await service.LinkAsync(organizationId, request, ct);
        return Ok(ApiResponse<VehicleResponse>.Success(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<VehicleResponse>>>> List(Guid organizationId, CancellationToken ct)
    {
        var result = await service.ListAsync(organizationId, ct);
        return Ok(ApiResponse<IReadOnlyList<VehicleResponse>>.Success(result));
    }

    [HttpPut("{linkId:guid}/suspension")]
    public async Task<ActionResult<ApiResponse<object>>> Suspend(Guid organizationId, Guid linkId, SuspendVehicleRequest request, CancellationToken ct)
    {
        await service.SuspendAsync(organizationId, linkId, request, ct);
        return Ok(ApiResponse<object>.Success(new { }));
    }

    [HttpPut("{linkId:guid}/profile")]
    public async Task<ActionResult<ApiResponse<VehicleResponse>>> UpdateProfile(Guid organizationId, Guid linkId, UpdateVehicleProfileRequest request, CancellationToken ct)
        => Ok(ApiResponse<VehicleResponse>.Success(await service.UpdateProfileAsync(organizationId, linkId, request, ct)));

    [HttpDelete("{linkId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> End(Guid organizationId, Guid linkId, EndVehicleRequest request, CancellationToken ct)
    {
        await service.EndAsync(organizationId, linkId, request, ct);
        return Ok(ApiResponse<object>.Success(new { }));
    }

}
