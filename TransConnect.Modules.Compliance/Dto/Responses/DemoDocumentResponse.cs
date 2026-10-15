namespace TransConnect.Modules.Compliance.Dto.Responses;

public sealed record DemoDocumentResponse(
    Guid Id, string Status, string ContentType, long Size, string Sha256,
    string Scenario, string ScanResult, string? OcrText,
    DateTime CreatedAt, DateTime? SimulatedAt, DateTime? ReviewedAt)
{
    public bool IsDemo => true;
    public string ProcessingMode => "SIMULATED";
    public string Verification => "NOT_VERIFIED";
    public string Notice => "DEMO: chưa quét mã độc, chưa OCR thật; chỉ dùng dữ liệu giả lập.";
}

public sealed record DemoDownloadResponse(byte[] Content, string FileName);
