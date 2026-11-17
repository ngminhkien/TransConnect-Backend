using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record SubmitOrganizationRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên tổ chức."), MaxLength(200, ErrorMessage = "Tên tối đa 200 ký tự.")]
    public string Name { get; init; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập mã đăng ký."), MaxLength(100, ErrorMessage = "Mã tối đa 100 ký tự.")]
    public string RegistrationNumber { get; init; } = string.Empty;
    [MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string OwnerEmail { get; init; } = string.Empty;
    // Bắt buộc ≥ 1 loại dịch vụ khi nộp/nộp lại (Q-M2-SERVICE-007B).
    public IReadOnlyList<string>? RequestedServiceTypes { get; init; }

    // Q-PARTNER-REG-D1 B: thông tin hồ sơ tùy chọn khi nộp, cùng quy tắc với UpdateOrganizationProfileRequest.
    // null/trống = không khai (nộp lại: giữ giá trị đang có). Không là điều kiện duyệt.
    [RegularExpression("^(Enterprise|Cooperative|HouseholdBusiness)$", ErrorMessage = "Loại chủ thể không hợp lệ.")]
    public string? LegalEntityType { get; init; }
    [MaxLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự.")]
    public string? RegisteredAddress { get; init; }
    [RegularExpression("^[+0-9 ()-]{6,20}$", ErrorMessage = "Số liên hệ không hợp lệ.")]
    public string? ContactPhone { get; init; }
    [MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string? ContactEmail { get; init; }
}
