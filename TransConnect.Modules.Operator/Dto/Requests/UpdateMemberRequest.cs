using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record UpdateMemberRequest
{
    [Required(ErrorMessage = "Vui lòng chọn vai trò."), RegularExpression("^(Manager|Dispatcher)$", ErrorMessage = "Vai trò phải là Manager hoặc Dispatcher.")]
    public string Role { get; init; } = "Dispatcher";
    public bool IsActive { get; init; } = true;
}
