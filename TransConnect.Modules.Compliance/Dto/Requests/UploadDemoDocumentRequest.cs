using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace TransConnect.Modules.Compliance.Dto.Requests;

public sealed class UploadDemoDocumentRequest
{
    [Required(ErrorMessage = "Vui lòng chọn file giả lập.")]
    public IFormFile File { get; set; } = null!;
    public bool SyntheticDataConfirmed { get; set; }
}
