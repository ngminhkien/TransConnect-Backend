using TransConnect.Modules.Operator.Dto.Requests;
using TransConnect.Modules.Operator.Dto.Responses;

namespace TransConnect.Modules.Operator.Services;

public interface IOrganizationService
{
    Task<OrganizationResponse> ResubmitAsync(Guid id, SubmitOrganizationRequest request, CancellationToken ct);
    Task<OrganizationResponse> SubmitAsync(SubmitOrganizationRequest request, CancellationToken ct);
    Task<IReadOnlyList<OrganizationResponse>> ListAsync(CancellationToken ct);
    Task<OrganizationResponse> GetAsync(Guid id, CancellationToken ct);
    Task<OrganizationResponse> UpdateProfileAsync(Guid id, UpdateOrganizationProfileRequest request, CancellationToken ct);
    Task<OrganizationResponse> ReviewAsync(Guid id, ReviewRequest request, CancellationToken ct);
    Task<InvitationResponse> InviteOwnerAsync(Guid id, CancellationToken ct);
    Task<InvitationResponse> InviteAsync(Guid id, InviteMemberRequest request, CancellationToken ct);
    Task<MemberResponse> AcceptAsync(AcceptInvitationRequest request, CancellationToken ct);
    Task RevokeAsync(Guid id, Guid invitationId, CancellationToken ct);
    Task<IReadOnlyList<MemberResponse>> MembersAsync(Guid id, CancellationToken ct);
    Task<MemberResponse> UpdateMemberAsync(Guid id, Guid memberId, UpdateMemberRequest request, CancellationToken ct);
    Task SetLimitAsync(Guid id, EmployeeLimitRequest request, CancellationToken ct);
    Task SetDriverLimitAsync(Guid id, DriverLimitRequest request, CancellationToken ct);
    Task<IReadOnlyList<AuditResponse>> AuditAsync(Guid id, CancellationToken ct);
}
