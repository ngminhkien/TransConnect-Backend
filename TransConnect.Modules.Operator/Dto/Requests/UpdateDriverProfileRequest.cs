using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record UpdateDriverProfileRequest
{
    [Required(ErrorMessage = "Vui lòng nhập số liên hệ."), RegularExpression("^[+0-9 ()-]{6,20}$", ErrorMessage = "Số liên hệ không hợp lệ.")]
    public string ContactPhone { get; init; } = "";
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ liên hệ."), MaxLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự.")]
    public string ContactAddress { get; init; } = "";
}
