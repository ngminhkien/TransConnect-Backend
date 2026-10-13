using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record LinkVehicleRequest
{
    [Required(ErrorMessage = "Vui lòng nhập biển số."), RegularExpression("^[a-zA-Z0-9 .-]{4,20}$", ErrorMessage = "Biển số không hợp lệ.")]
    public string RegistrationPlate { get; init; } = string.Empty;
    [MaxLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
    public string Description { get; init; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập căn cứ xác minh quyền khai thác."), MaxLength(1000, ErrorMessage = "Căn cứ tối đa 1000 ký tự.")]
    public string VerificationReference { get; init; } = string.Empty;
}
