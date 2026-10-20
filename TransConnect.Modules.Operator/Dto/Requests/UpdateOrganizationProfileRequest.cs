using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record UpdateOrganizationProfileRequest
{
    [Required(ErrorMessage = "Vui lòng chọn loại chủ thể."), RegularExpression("^(Enterprise|Cooperative|HouseholdBusiness)$", ErrorMessage = "Loại chủ thể không hợp lệ.")]
    public string LegalEntityType { get; init; } = "";
    [Required(ErrorMessage = "Vui lòng nhập địa chỉ đăng ký."), MaxLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự.")]
    public string RegisteredAddress { get; init; } = "";
    [Required(ErrorMessage = "Vui lòng nhập số liên hệ."), RegularExpression("^[+0-9 ()-]{6,20}$", ErrorMessage = "Số liên hệ không hợp lệ.")]
    public string ContactPhone { get; init; } = "";
    [Required(ErrorMessage = "Vui lòng nhập email liên hệ."), EmailAddress(ErrorMessage = "Email không hợp lệ."), MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string ContactEmail { get; init; } = "";
}
