using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using TransConnect.Modules.Notification.Options;

namespace TransConnect.Modules.Notification.Templates;

// Đọc template từ Embedded Resource, ghép vào _Layout.html rồi thay placeholder {{Key}}.
// Cố tình giữ đơn giản (không Razor, không thư viện ngoài): nhu cầu hiện tại chỉ là thay chuỗi.
public partial class EmbeddedTemplateRenderer(IOptions<EmailOptions> emailOptions)
    : ITemplateRenderer
{
    private const string BodyPlaceholder = "{{Body}}";

    private readonly EmailBrandingOptions _branding = emailOptions.Value.Branding;
    private static readonly ConcurrentDictionary<string, string> ResourceCache = new();

    [GeneratedRegex(@"<!--\s*subject:\s*(?<value>.*?)\s*-->", RegexOptions.IgnoreCase)]
    private static partial Regex SubjectDirective();

    [GeneratedRegex(@"<!--\s*preheader:\s*(?<value>.*?)\s*-->", RegexOptions.IgnoreCase)]
    private static partial Regex PreheaderDirective();

    [GeneratedRegex(@"\{\{(?<key>\w+)\}\}")]
    private static partial Regex Placeholder();

    public RenderedEmail Render(string templateCode, IReadOnlyDictionary<string, string> placeholders)
    {
        var rawHtml = ReadTemplate($"{templateCode}.html");
        var rawText = ReadTemplate($"{templateCode}.txt");

        var subjectTemplate = SubjectDirective().Match(rawHtml) is { Success: true } m
            ? m.Groups["value"].Value
            : _branding.AppName;

        var preheaderTemplate = PreheaderDirective().Match(rawHtml) is { Success: true } p
            ? p.Groups["value"].Value
            : string.Empty;

        // Bỏ 2 dòng directive khỏi phần thân trước khi render.
        var bodyTemplate = PreheaderDirective().Replace(SubjectDirective().Replace(rawHtml, string.Empty), string.Empty);

        // Giá trị do người dùng nhập (FullName...) PHẢI được HTML-encode trước khi chèn vào
        // HTML, nếu không một cái tên chứa thẻ HTML sẽ phá vỡ bố cục email.
        var htmlValues = BuildValues(placeholders, htmlEncode: true);
        htmlValues["HeaderBrand"] = BuildHeaderBrand();
        htmlValues["FooterMeta"] = BuildFooterMeta();

        var subject = Fill(subjectTemplate, BuildValues(placeholders, htmlEncode: false));
        var preheader = Fill(preheaderTemplate, htmlValues);
        var bodyHtml = Fill(bodyTemplate, htmlValues);

        htmlValues["Subject"] = WebUtility.HtmlEncode(subject);
        htmlValues["Preheader"] = preheader;

        // Chèn phần thân bằng string.Replace SAU khi đã render layout, để nội dung thân
        // (có thể chứa dữ liệu người dùng) không bị đem đi thay placeholder lần hai.
        var layout = Fill(ReadTemplate("_Layout.html"), htmlValues, keepUnresolved: BodyPlaceholder);
        var html = layout.Replace(BodyPlaceholder, bodyHtml);

        // Bản plain-text không nằm trong HTML nên không encode.
        var text = Fill(rawText, BuildValues(placeholders, htmlEncode: false));

        return new RenderedEmail(subject, html, text);
    }

    private Dictionary<string, string> BuildValues(
        IReadOnlyDictionary<string, string> placeholders,
        bool htmlEncode)
    {
        string Encode(string value) => htmlEncode ? WebUtility.HtmlEncode(value) : value;

        // Branding luôn có sẵn cho mọi template, không cần bên gọi truyền vào.
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AppName"] = Encode(_branding.AppName),
            ["PrimaryColor"] = _branding.PrimaryColor,
            ["LogoUrl"] = _branding.LogoUrl,
            ["CompanyName"] = Encode(_branding.CompanyName),
            ["CompanyAddress"] = Encode(_branding.CompanyAddress),
            ["SupportEmail"] = Encode(_branding.SupportEmail),
            ["WebsiteUrl"] = _branding.WebsiteUrl
        };

        foreach (var (key, value) in placeholders)
        {
            values[key] = Encode(value ?? string.Empty);
        }

        return values;
    }

    // Chưa cấu hình logo thì hiện tên app bằng chữ — email vẫn tử tế, không vỡ bố cục,
    // không có ô ảnh lỗi. Thêm logo sau chỉ cần điền Email:Branding:LogoUrl.
    private string BuildHeaderBrand() =>
        string.IsNullOrWhiteSpace(_branding.LogoUrl)
            ? $"""
               <div style="font-family:Arial,Helvetica,sans-serif; font-size:22px; font-weight:bold; color:#ffffff;">
                 {WebUtility.HtmlEncode(_branding.AppName)}
               </div>
               """
            : $"""
               <img src="{_branding.LogoUrl}" alt="{WebUtility.HtmlEncode(_branding.AppName)}"
                    height="40" style="display:block; border:0; max-height:40px;" />
               """;

    private string BuildFooterMeta()
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(_branding.CompanyName))
        {
            lines.Add($"<div><strong>{WebUtility.HtmlEncode(_branding.CompanyName)}</strong></div>");
        }

        if (!string.IsNullOrWhiteSpace(_branding.CompanyAddress))
        {
            lines.Add($"<div>{WebUtility.HtmlEncode(_branding.CompanyAddress)}</div>");
        }

        if (!string.IsNullOrWhiteSpace(_branding.SupportEmail))
        {
            lines.Add($"""<div>Hỗ trợ: <a href="mailto:{_branding.SupportEmail}" style="color:#6b7280;">{WebUtility.HtmlEncode(_branding.SupportEmail)}</a></div>""");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Fill(
        string template,
        IReadOnlyDictionary<string, string> values,
        string? keepUnresolved = null)
    {
        return Placeholder().Replace(template, match =>
        {
            if (keepUnresolved is not null && match.Value == keepUnresolved)
            {
                return match.Value;
            }

            // Placeholder không có giá trị thì thay bằng rỗng, tuyệt đối không để lộ
            // chuỗi {{Xxx}} ra email gửi cho người dùng.
            return values.TryGetValue(match.Groups["key"].Value, out var value) ? value : string.Empty;
        });
    }

    private static string ReadTemplate(string fileName) =>
        ResourceCache.GetOrAdd(fileName, static name =>
        {
            var assembly = typeof(EmbeddedTemplateRenderer).GetTypeInfo().Assembly;

            // Dò theo hậu tố thay vì ghép cứng root namespace, để đổi tên namespace
            // không làm hỏng việc đọc template.
            var resourceName = Array.Find(
                assembly.GetManifestResourceNames(),
                r => r.EndsWith($".Templates.{name}", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Không tìm thấy template '{name}'. Kiểm tra file đã nằm trong Templates/ và " +
                    "được khai báo <EmbeddedResource> trong .csproj chưa.");

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        });
}
