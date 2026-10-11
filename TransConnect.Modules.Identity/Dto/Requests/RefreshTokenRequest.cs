using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Identity.Dto.Requests;

public record RefreshTokenRequest
{
    [Required(ErrorMessage = "Vui lòng nhập {0}.")]
    [Display(Name = "Mã làm mới phiên")]
    public string RefreshToken { get; init; } = string.Empty;
}
