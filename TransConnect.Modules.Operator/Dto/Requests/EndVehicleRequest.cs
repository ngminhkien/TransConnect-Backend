using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed class EndVehicleRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do kết thúc liên kết xe.")]
    [StringLength(1000, ErrorMessage = "Lý do không được vượt quá 1000 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}
