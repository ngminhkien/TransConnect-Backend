using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Identity.Dto.Requests;

public record ForgotPasswordRequest
{
    [RegularExpression("^(Customer|Operator|InternalAdmin)$", ErrorMessage = "Nhóm tài khoản không hợp lệ.")]
    [Required(ErrorMessage = "Vui lòng chọn nhóm tài khoản.")]
    public string AccountGroup { get; init; } = "Customer";

    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [MaxLength(256, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Email")]
    public string Email { get; init; } = string.Empty;
}
