using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record SubmitDriverRequest
{
    [Required(ErrorMessage = "Vui lòng nhập họ tên."), MaxLength(200, ErrorMessage = "Họ tên tối đa 200 ký tự.")]
    public string FullName { get; init; } = string.Empty;
}
