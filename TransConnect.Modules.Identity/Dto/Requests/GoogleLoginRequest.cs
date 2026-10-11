using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Identity.Dto.Requests;

public record GoogleLoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [Display(Name = "Mã đăng nhập Google")]
    public string IdToken { get; init; } = string.Empty;
}
