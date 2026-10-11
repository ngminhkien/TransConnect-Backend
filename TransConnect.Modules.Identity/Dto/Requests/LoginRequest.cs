using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Identity.Dto.Requests;

public record LoginRequest
{
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [Display(Name = "Email")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [Display(Name = "Mật khẩu")]
    public string Password { get; init; } = string.Empty;
}
