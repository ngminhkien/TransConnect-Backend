using System.ComponentModel.DataAnnotations;

namespace TransConnect.Modules.Compliance.Dto.Requests;

public sealed class SimulateDocumentRequest
{
    [Required(ErrorMessage = "Vui lòng chọn kịch bản mô phỏng.")]
    [RegularExpression("^(Success|Infected|ScannerUnavailable|OcrFailed)$", ErrorMessage = "Kịch bản mô phỏng không hợp lệ.")]
    public string Scenario { get; set; } = "Success";
}

public sealed class ReviewDemoDocumentRequest
{
    [Required(ErrorMessage = "Vui lòng chọn kết quả duyệt demo.")]
    public bool? Approved { get; set; }
}
