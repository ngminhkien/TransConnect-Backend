using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Identity.Dto.Requests;

public record VerifyEmailRequest
{
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [MaxLength(256, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Email")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(10, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Mã")]
    public string Code { get; init; } = string.Empty;
}
