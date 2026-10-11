using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Auth;
using TransConnect.Common.Response;
using TransConnect.Modules.Identity.Dto.Requests;
using TransConnect.Modules.Identity.Dto.Responses;
using TransConnect.Modules.Identity.Services;

namespace TransConnect.Modules.Identity.Controllers;

[ApiController]
[Route("roles")]
[Authorize]
[RequirePermission("roles.manage")]
public class RolesController(IRoleService roleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RoleResponse>>>> GetAll(CancellationToken cancellationToken)
    {
        var roles = await roleService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RoleResponse>>.Success(roles));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RoleResponse>>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var role = await roleService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<RoleResponse>.Success(role));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<RoleResponse>>> Create(
        CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await roleService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<RoleResponse>.Success(role));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RoleResponse>>> Update(
        Guid id, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var role = await roleService.UpdateAsync(id, request, cancellationToken);
        return Ok(ApiResponse<RoleResponse>.Success(role));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken cancellationToken)
    {
        await roleService.DeleteAsync(id, cancellationToken);
        return Ok(ApiResponse<object>.Success(new { }));
    }

    [HttpPost("{roleId:guid}/permissions/{permissionId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> AssignPermission(
        Guid roleId, Guid permissionId, CancellationToken cancellationToken)
    {
        await roleService.AssignPermissionAsync(roleId, permissionId, cancellationToken);
        return Ok(ApiResponse<object>.Success(new { }));
    }

    [HttpDelete("{roleId:guid}/permissions/{permissionId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> RemovePermission(
        Guid roleId, Guid permissionId, CancellationToken cancellationToken)
    {
        await roleService.RemovePermissionAsync(roleId, permissionId, cancellationToken);
        return Ok(ApiResponse<object>.Success(new { }));
    }

    [HttpPost("{roleId:guid}/users/{userId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> AssignToUser(
        Guid roleId, Guid userId, CancellationToken cancellationToken)
    {
        await roleService.AssignToUserAsync(roleId, userId, cancellationToken);
        return Ok(ApiResponse<object>.Success(new { }));
    }

    [HttpDelete("{roleId:guid}/users/{userId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveFromUser(
        Guid roleId, Guid userId, CancellationToken cancellationToken)
    {
        await roleService.RemoveFromUserAsync(roleId, userId, cancellationToken);
        return Ok(ApiResponse<object>.Success(new { }));
    }
}
