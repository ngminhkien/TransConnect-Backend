using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Identity.Dto.Requests;

public record GoogleLoginRequest
{
    [RegularExpression("^(Customer|Operator|InternalAdmin)$", ErrorMessage = "Nhóm tài khoản không hợp lệ.")]
    [Required(ErrorMessage = "Vui lòng chọn nhóm tài khoản.")]
    public string AccountGroup { get; init; } = "Customer";

    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [Display(Name = "Mã đăng nhập Google")]
    public string IdToken { get; init; } = string.Empty;
}
