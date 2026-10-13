using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Operator.Dto.Requests;

public sealed record DriverLimitRequest
{
    [Range(1, 100000, ErrorMessage = "Hạn mức phải từ 1 đến 100000 hoặc để trống.")]
    public int? Limit { get; init; }
}
