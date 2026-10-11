using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Identity.Dto.Requests;

public record RegisterRequest
{
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [MaxLength(256, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Email")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MinLength(6, ErrorMessage = "{0} phải có ít nhất {1} ký tự.")]
    [MaxLength(100, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Mật khẩu")]
    public string Password { get; init; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(200, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; init; } = string.Empty;

    [MaxLength(20, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; init; }
}
