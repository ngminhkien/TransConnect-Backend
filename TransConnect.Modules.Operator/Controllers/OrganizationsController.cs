using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TransConnect.Common.Response;
using TransConnect.Modules.Operator.Dto.Requests;
using TransConnect.Modules.Operator.Dto.Responses;
using TransConnect.Modules.Operator.Services;

namespace TransConnect.Modules.Operator.Controllers;

[ApiController]
[Authorize]
[Route("organizations")]
public sealed class OrganizationsController(IOrganizationService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> Submit(SubmitOrganizationRequest request, CancellationToken ct)
    {
        var result = await service.SubmitAsync(request, ct);
        return Ok(ApiResponse<OrganizationResponse>.Success(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OrganizationResponse>>>> List(CancellationToken ct)
    {
        var result = await service.ListAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<OrganizationResponse>>.Success(result));
    }

    [HttpPost("{id:guid}/resubmit")]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> Resubmit(Guid id, SubmitOrganizationRequest request, CancellationToken ct) =>
        Ok(ApiResponse<OrganizationResponse>.Success(await service.ResubmitAsync(id, request, ct)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> Get(Guid id, CancellationToken ct)
    {
        var result = await service.GetAsync(id, ct);
        return Ok(ApiResponse<OrganizationResponse>.Success(result));
    }

    [HttpPut("{id:guid}/profile")]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> UpdateProfile(Guid id, UpdateOrganizationProfileRequest request, CancellationToken ct)
        => Ok(ApiResponse<OrganizationResponse>.Success(await service.UpdateProfileAsync(id, request, ct)));

    [HttpPost("{id:guid}/review")]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> Review(Guid id, ReviewRequest request, CancellationToken ct)
    {
        var result = await service.ReviewAsync(id, request, ct);
        return Ok(ApiResponse<OrganizationResponse>.Success(result));
    }

    [HttpPost("{id:guid}/owner-invitation")]
    public async Task<ActionResult<ApiResponse<InvitationResponse>>> InviteOwner(Guid id, CancellationToken ct)
    {
        var result = await service.InviteOwnerAsync(id, ct);
        return Ok(ApiResponse<InvitationResponse>.Success(result));
    }

    [HttpPost("{id:guid}/invitations")]
    public async Task<ActionResult<ApiResponse<InvitationResponse>>> Invite(Guid id, InviteMemberRequest request, CancellationToken ct)
    {
        var result = await service.InviteAsync(id, request, ct);
        return Ok(ApiResponse<InvitationResponse>.Success(result));
    }

    [HttpPost("invitations/accept")]
    public async Task<ActionResult<ApiResponse<MemberResponse>>> Accept(AcceptInvitationRequest request, CancellationToken ct)
    {
        var result = await service.AcceptAsync(request, ct);
        return Ok(ApiResponse<MemberResponse>.Success(result));
    }

    [HttpDelete("{id:guid}/invitations/{invitationId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Revoke(Guid id, Guid invitationId, CancellationToken ct)
    {
        await service.RevokeAsync(id, invitationId, ct);
        return Ok(ApiResponse<object>.Success(new { }));
    }

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MemberResponse>>>> Members(Guid id, CancellationToken ct)
    {
        var result = await service.MembersAsync(id, ct);
        return Ok(ApiResponse<IReadOnlyList<MemberResponse>>.Success(result));
    }

    [HttpPut("{id:guid}/members/{memberId:guid}")]
    public async Task<ActionResult<ApiResponse<MemberResponse>>> UpdateMember(Guid id, Guid memberId, UpdateMemberRequest request, CancellationToken ct)
    {
        var result = await service.UpdateMemberAsync(id, memberId, request, ct);
        return Ok(ApiResponse<MemberResponse>.Success(result));
    }

    [HttpPut("{id:guid}/employee-limit")]
    public async Task<ActionResult<ApiResponse<object>>> SetLimit(Guid id, EmployeeLimitRequest request, CancellationToken ct)
    {
        await service.SetLimitAsync(id, request, ct);
        return Ok(ApiResponse<object>.Success(new { }));
    }

    [HttpPut("{id:guid}/driver-limit")]
    public async Task<ActionResult<ApiResponse<object>>> SetDriverLimit(Guid id, DriverLimitRequest request, CancellationToken ct)
    {
        await service.SetDriverLimitAsync(id, request, ct);
        return Ok(ApiResponse<object>.Success(new { }));
    }

    [HttpGet("{id:guid}/audit")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AuditResponse>>>> Audit(Guid id, CancellationToken ct)
    {
        var result = await service.AuditAsync(id, ct);
        return Ok(ApiResponse<IReadOnlyList<AuditResponse>>.Success(result));
    }

}
