using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Response;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Dto.Responses;
using TransConnect.Modules.Identity.Services;

namespace TransConnect.Modules.Identity.Controllers;

[ApiController, Authorize, Route("accounts")]
public sealed class AccountsController(AccountManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AccountResponse>>>> List(CancellationToken ct,
        int pageNumber = 1, int pageSize = 20, string? search = null) =>
        Ok(ApiResponse<PagedResult<AccountResponse>>.Success(await service.ListAsync(pageNumber, pageSize, search, ct)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AccountResponse>>> Get(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<AccountResponse>.Success(await service.GetAsync(id, ct)));

    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<ApiResponse<PagedResult<AccountAuditResponse>>>> Audit(Guid id, CancellationToken ct,
        int pageNumber = 1, int pageSize = 20) =>
        Ok(ApiResponse<PagedResult<AccountAuditResponse>>.Success(await service.AuditAsync(id, pageNumber, pageSize, ct)));

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<ApiResponse<AccountResponse>>> Status(Guid id, SetAccountStatusRequest request, CancellationToken ct) =>
        Ok(ApiResponse<AccountResponse>.Success(await service.SetStatusAsync(id, request, ct)));
}
