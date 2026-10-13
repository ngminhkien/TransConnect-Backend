using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Identity.Dto.Requests;

public record LoginRequest
{
    [RegularExpression("^(Customer|Operator|InternalAdmin)$", ErrorMessage = "Nhóm tài khoản không hợp lệ.")]
    [Required(ErrorMessage = "Vui lòng chọn nhóm tài khoản.")]
    public string AccountGroup { get; init; } = "Customer";

    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [Display(Name = "Email")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [Display(Name = "Mật khẩu")]
    public string Password { get; init; } = string.Empty;
}
