namespace TransConnect.Host.Options;

// Chỉ Host cần cấu hình này (module không tự bật CORS), nên để ở Host thay vì Common —
// xem checklist trong docs/base-libraries-guidelines.md.
public class CorsOptions
{
    public const string SectionName = "Cors";

    // Danh sách origin của client được phép gọi API, vd "http://localhost:5173" (React dev),
    // "https://transconnect.vn" (web production).
    //
    // Lưu ý: app Flutter chạy native (Android/iOS) KHÔNG bị CORS chi phối — CORS là cơ chế
    // của trình duyệt. Chỉ cần khai origin ở đây khi build Flutter Web.
    public string[] AllowedOrigins { get; set; } = [];
}
