namespace TransConnect.Modules.Notification.Templates;

public record RenderedEmail(string Subject, string BodyHtml, string BodyText);

// Tách interface để sau này đổi nguồn template (từ file nhúng sang bảng trong DB, cho phép
// sửa nội dung mà không cần deploy lại) chỉ phải thay implementation.
public interface ITemplateRenderer
{
    RenderedEmail Render(string templateCode, IReadOnlyDictionary<string, string> placeholders);
}
