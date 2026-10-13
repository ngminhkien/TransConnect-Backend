using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record AcceptInvitationRequest
{
    [Required(ErrorMessage = "Vui lòng nhập token lời mời."), MaxLength(128, ErrorMessage = "Token không hợp lệ.")]
    public string Token { get; init; } = string.Empty;
}
