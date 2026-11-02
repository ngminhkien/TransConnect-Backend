using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Auth;
using TransConnect.Modules.Identity.Options;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Services;

public sealed class AvatarService(IdentityDbContext db, ICurrentUser current, HttpClient http,
    IOptions<CloudinaryOptions> options)
{
    public async Task<string> UploadAsync(IFormFile file, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == current.UserId && x.IsActive, ct)
            ?? throw new UnauthorizedAppException("Vui lòng đăng nhập.");
        if (user.AccountGroup is not (AccountGroups.Customer or AccountGroups.Operator))
            throw new ForbiddenAppException();
        if (file.Length is <= 0 or > 5242880)
            throw new ValidationAppException("Ảnh đại diện phải từ 1 byte đến 5 MB.");
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.CloudName) || string.IsNullOrWhiteSpace(config.ApiKey) ||
            string.IsNullOrWhiteSpace(config.ApiSecret))
            throw new ServiceUnavailableAppException("Chưa cấu hình Cloudinary.");
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        var read = await stream.ReadAsync(header, ct);
        var png = read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
        var webp = read == 12 && Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && Encoding.ASCII.GetString(header, 8, 4) == "WEBP";
        if (!png && !jpeg && !webp) throw new ValidationAppException("Chỉ nhận ảnh PNG, JPEG hoặc WebP.");
        stream.Position = 0;
        // Stable public ID prevents accumulating avatars; provider re-encodes to JPEG.
        var root = config.RootFolder;
        if (string.IsNullOrWhiteSpace(root) || root.Length > 100 || root.Split('/').Any(segment =>
            segment.Length == 0 || !segment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')))
            throw new ServiceUnavailableAppException("Cấu hình thư mục Cloudinary không hợp lệ.");
        var folder = $"{root}/avatars/{user.AccountGroup.ToLowerInvariant()}";
        var publicId = $"{folder}/{user.Id:N}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["public_id"] = publicId, ["timestamp"] = timestamp, ["overwrite"] = "true",
            ["invalidate"] = "true", ["transformation"] = "c_limit,h_512,w_512/f_jpg"
        };
        // Dynamic folders separate Media Library placement from the public ID.
        // Fixed folders derive placement from the public ID path.
        if (config.UseDynamicFolders) parameters["asset_folder"] = folder;
        var signature = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("&", parameters.Select(x => $"{x.Key}={x.Value}")) + config.ApiSecret))).ToLowerInvariant();
        using var body = new MultipartFormDataContent();
        foreach (var pair in parameters) body.Add(new StringContent(pair.Value), pair.Key);
        body.Add(new StringContent(config.ApiKey), "api_key");
        body.Add(new StringContent(signature), "signature");
        body.Add(new StreamContent(stream), "file", "avatar");
        try
        {
            using var response = await http.PostAsync(
                $"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(config.CloudName)}/image/upload", body, ct);
            if (!response.IsSuccessStatusCode) throw new ServiceUnavailableAppException("Không thể lưu ảnh đại diện.");
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var url = json.RootElement.TryGetProperty("secure_url", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() : null;
            if (url is null || url.Length > 500 || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || uri.Scheme != "https" || uri.Host != "res.cloudinary.com")
                throw new ServiceUnavailableAppException("Phản hồi lưu ảnh không hợp lệ.");
            var count = await db.Users.Where(x => x.Id == user.Id && x.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.AvatarUrl, url).SetProperty(x => x.UpdatedAt, DateTime.UtcNow), ct);
            if (count != 1) throw new UnauthorizedAppException("Tài khoản không còn khả dụng.");
            return url;
        }
        catch (HttpRequestException) { throw new ServiceUnavailableAppException("Dịch vụ lưu ảnh tạm thời không khả dụng."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new ServiceUnavailableAppException("Dịch vụ lưu ảnh hết thời gian chờ."); }
        catch (JsonException) { throw new ServiceUnavailableAppException("Phản hồi lưu ảnh không hợp lệ."); }
    }
}
