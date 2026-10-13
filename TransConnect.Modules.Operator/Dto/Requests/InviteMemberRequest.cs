using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record InviteMemberRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email."), EmailAddress(ErrorMessage = "Email không hợp lệ."), MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string Email { get; init; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng chọn vai trò."), RegularExpression("^(Manager|Dispatcher)$", ErrorMessage = "Vai trò phải là Manager hoặc Dispatcher.")]
    public string Role { get; init; } = "Dispatcher";
}
