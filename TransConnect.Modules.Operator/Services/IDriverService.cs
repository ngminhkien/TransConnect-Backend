using TransConnect.Modules.Operator.Dto.Requests;
using TransConnect.Modules.Operator.Dto.Responses;

namespace TransConnect.Modules.Operator.Services;

public interface IDriverService
{
    Task<DriverResponse> SubmitAsync(SubmitDriverRequest request, CancellationToken ct);
    Task<DriverResponse> MeAsync(CancellationToken ct);
    Task<IReadOnlyList<DriverResponse>> PendingAsync(CancellationToken ct);
    Task<DriverResponse> ReviewAsync(Guid id, ReviewRequest request, CancellationToken ct);
    Task<InvitationResponse> InviteAsync(Guid organizationId, Guid driverId, CancellationToken ct);
    Task<InvitationResponse> InviteByEmailAsync(Guid organizationId, InviteDriverRequest request, CancellationToken ct);
    Task<EngagementResponse> AcceptAsync(AcceptInvitationRequest request, CancellationToken ct);
    Task<IReadOnlyList<EngagementResponse>> ListAsync(Guid organizationId, CancellationToken ct);
    Task EndAsync(Guid organizationId, Guid engagementId, CancellationToken ct);
}
