using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Response;
using TransConnect.Modules.Operator.Services;

namespace TransConnect.Modules.Operator.Controllers;

[ApiController, Authorize]
public sealed class FoundationContextController(FoundationReadService service) : ControllerBase
{
    [HttpGet("operator/context")]
    public async Task<ActionResult<ApiResponse<FoundationContext>>> Context(CancellationToken ct) =>
        Ok(ApiResponse<FoundationContext>.Success(await service.ContextAsync(ct)));
    [HttpGet("operator/invitations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InvitationItem>>>> MyInvitations(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<InvitationItem>>.Success(await service.InvitationsAsync(null, ct)));
    [HttpGet("organizations/{id:guid}/invitations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<InvitationItem>>>> Invitations(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<InvitationItem>>.Success(await service.InvitationsAsync(id, ct)));
    [HttpGet("drivers/me/collaborations")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<DriverCollaboration>>>> Collaborations(CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<DriverCollaboration>>.Success(await service.CollaborationsAsync(ct)));
    [HttpGet("organizations/{id:guid}/reviews")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ReviewHistoryItem>>>> OrganizationHistory(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<ReviewHistoryItem>>.Success(await service.HistoryAsync("Organization", id, ct)));
    [HttpGet("drivers/{id:guid}/reviews")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<ReviewHistoryItem>>>> DriverHistory(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<IReadOnlyList<ReviewHistoryItem>>.Success(await service.HistoryAsync("Driver", id, ct)));
}
