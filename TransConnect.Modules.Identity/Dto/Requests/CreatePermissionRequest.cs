using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Identity.Dto.Requests;

public record CreatePermissionRequest
{
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [MaxLength(150, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Mã")]
    public string Code { get; init; } = string.Empty;

    [MaxLength(500, ErrorMessage = "{0} không được vượt quá {1} ký tự.")]
    [Display(Name = "Mô tả")]
    public string? Description { get; init; }
}
