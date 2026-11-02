using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record UpdateVehicleProfileRequest : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng chọn loại xe."), RegularExpression("^(Passenger|Freight)$", ErrorMessage = "Loại xe không hợp lệ.")]
    public string VehicleKind { get; init; } = "";
    [Range(1, 1000, ErrorMessage = "Số chỗ phải từ 1 đến 1000, gồm lái xe.")]
    public int? SeatCount { get; init; }
    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "Tải hàng cho phép phải lớn hơn 0 và không vượt 1.000.000 kg.")]
    public decimal? PermittedPayloadKg { get; init; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (VehicleKind == "Passenger" && (SeatCount is null || PermittedPayloadKg is not null))
            yield return new("Xe chở người cần số chỗ; tải hàng để trống.", [nameof(SeatCount), nameof(PermittedPayloadKg)]);
        if (VehicleKind == "Freight" && (PermittedPayloadKg is null || SeatCount is not null))
            yield return new("Xe chở hàng cần tải hàng cho phép (kg); số chỗ để trống trong demo.", [nameof(PermittedPayloadKg), nameof(SeatCount)]);
    }
}
