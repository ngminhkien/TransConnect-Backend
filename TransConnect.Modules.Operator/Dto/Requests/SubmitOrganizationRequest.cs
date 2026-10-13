using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record SubmitOrganizationRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên tổ chức."), MaxLength(200, ErrorMessage = "Tên tối đa 200 ký tự.")]
    public string Name { get; init; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập mã đăng ký."), MaxLength(100, ErrorMessage = "Mã tối đa 100 ký tự.")]
    public string RegistrationNumber { get; init; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng nhập email chủ tổ chức."), EmailAddress(ErrorMessage = "Email không hợp lệ."), MaxLength(256, ErrorMessage = "Email tối đa 256 ký tự.")]
    public string OwnerEmail { get; init; } = string.Empty;
}
