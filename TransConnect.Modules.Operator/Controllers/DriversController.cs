using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Response;
using TransConnect.Modules.Operator.Dto.Requests;
using TransConnect.Modules.Operator.Dto.Responses;
using TransConnect.Modules.Operator.Services;

namespace TransConnect.Modules.Operator.Controllers;

[ApiController]
[Authorize]
[Route("drivers")]
public sealed class DriversController(IDriverService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<DriverResponse>>> Submit(SubmitDriverRequest request, CancellationToken ct)
    {
        var result = await service.SubmitAsync(request, ct);
        return Ok(ApiResponse<DriverResponse>.Success(result));
    }

    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<DriverResponse>>> Me(CancellationToken ct)
    {
        var result = await service.MeAsync(ct);
        return Ok(ApiResponse<DriverResponse>.Success(result));
    }

    [HttpGet("pending")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DriverResponse>>>> Pending(CancellationToken ct)
    {
        var result = await service.PendingAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<DriverResponse>>.Success(result));
    }

    [HttpPost("{id:guid}/review")]
    public async Task<ActionResult<ApiResponse<DriverResponse>>> Review(Guid id, ReviewRequest request, CancellationToken ct)
    {
        var result = await service.ReviewAsync(id, request, ct);
        return Ok(ApiResponse<DriverResponse>.Success(result));
    }

    [HttpPost("invitations/accept")]
    public async Task<ActionResult<ApiResponse<EngagementResponse>>> Accept(AcceptInvitationRequest request, CancellationToken ct)
    {
        var result = await service.AcceptAsync(request, ct);
        return Ok(ApiResponse<EngagementResponse>.Success(result));
    }

    [HttpPost("/organizations/{organizationId:guid}/drivers/{driverId:guid}/invitations")]
    public async Task<ActionResult<ApiResponse<InvitationResponse>>> Invite(Guid organizationId, Guid driverId, CancellationToken ct)
    {
        var result = await service.InviteAsync(organizationId, driverId, ct);
        return Ok(ApiResponse<InvitationResponse>.Success(result));
    }

    [HttpPost("/organizations/{organizationId:guid}/driver-invitations")]
    public async Task<ActionResult<ApiResponse<InvitationResponse>>> InviteByEmail(Guid organizationId, InviteDriverRequest request, CancellationToken ct)
    {
        var result = await service.InviteByEmailAsync(organizationId, request, ct);
        return Ok(ApiResponse<InvitationResponse>.Success(result));
    }

    [HttpGet("/organizations/{organizationId:guid}/drivers")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EngagementResponse>>>> List(Guid organizationId, CancellationToken ct)
    {
        var result = await service.ListAsync(organizationId, ct);
        return Ok(ApiResponse<IReadOnlyList<EngagementResponse>>.Success(result));
    }

    [HttpDelete("/organizations/{organizationId:guid}/drivers/{engagementId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> End(Guid organizationId, Guid engagementId, CancellationToken ct)
    {
        await service.EndAsync(organizationId, engagementId, ct);
        return Ok(ApiResponse<object>.Success(new { }));
    }

}
