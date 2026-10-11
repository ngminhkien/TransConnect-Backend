using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Auth;
using TransConnect.Common.Response;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Dto.Responses;
using TransConnect.Modules.Identity.Services;

namespace TransConnect.Modules.Identity.Controllers;

[ApiController]
[Route("permissions")]
[Authorize]
[RequirePermission("permissions.manage")]
public class PermissionsController(IPermissionService permissionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PermissionResponse>>>> GetAll(CancellationToken cancellationToken)
    {
        var permissions = await permissionService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PermissionResponse>>.Success(permissions));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PermissionResponse>>> Create(
        CreatePermissionRequest request, CancellationToken cancellationToken)
    {
        var permission = await permissionService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<PermissionResponse>.Success(permission));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await permissionService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Success(new { }));
    }
}
