using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record InviteDriverRequest
{
    [Required(ErrorMessage = "Vui lòng nhập email tài xế.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string Email { get; init; } = string.Empty;
}
