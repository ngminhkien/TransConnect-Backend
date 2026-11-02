using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Response;
using TransConnect.Modules.Operator.Dto.Requests;
using TransConnect.Modules.Operator.Dto.Responses;
using TransConnect.Modules.Operator.Services;

namespace TransConnect.Modules.Operator.Controllers;

[ApiController, Authorize]
public sealed class FoundationSearchController(FoundationSearchService service) : ControllerBase
{
    [HttpGet("organizations/search")]
    public async Task<ActionResult<ApiResponse<PagedResult<OrganizationResponse>>>> Organizations([FromQuery] FoundationSearchRequest request, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<OrganizationResponse>>.Success(await service.OrganizationsAsync(request, ct)));
    [HttpGet("drivers/search")]
    public async Task<ActionResult<ApiResponse<PagedResult<DriverResponse>>>> Drivers([FromQuery] FoundationSearchRequest request, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<DriverResponse>>.Success(await service.DriversAsync(request, ct)));
    [HttpGet("drivers/{id:guid}")]
    public async Task<ActionResult<ApiResponse<DriverResponse>>> Driver(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<DriverResponse>.Success(await service.DriverAsync(id, ct)));
    [HttpGet("organizations/{id:guid}/members/search")]
    public async Task<ActionResult<ApiResponse<PagedResult<MemberSummary>>>> Members(Guid id, [FromQuery] FoundationSearchRequest request, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<MemberSummary>>.Success(await service.MembersAsync(id, request, ct)));
    [HttpGet("organizations/{id:guid}/drivers/search")]
    public async Task<ActionResult<ApiResponse<PagedResult<EngagementSummary>>>> Engagements(Guid id, [FromQuery] FoundationSearchRequest request, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<EngagementSummary>>.Success(await service.EngagementsAsync(id, request, ct)));
    [HttpGet("organizations/{id:guid}/audit/search")]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditResponse>>>> Audit(Guid id, [FromQuery] FoundationSearchRequest request, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<AuditResponse>>.Success(await service.AuditAsync(id, request, ct)));
}
