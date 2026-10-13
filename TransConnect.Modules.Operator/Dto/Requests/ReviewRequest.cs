using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record ReviewRequest
{
    public bool Approved { get; init; }
    [Required(ErrorMessage = "Vui lòng nhập căn cứ thẩm định."), MaxLength(2000, ErrorMessage = "Nội dung tối đa 2000 ký tự.")]
    public string Note { get; init; } = string.Empty;
}
