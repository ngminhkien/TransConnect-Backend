using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Locations.Dto.Responses;

namespace TransConnect.Modules.Locations.Services;

// Singleton: all users share one outbound limiter/cache. Public Nominatim deployment is single-instance;
// multiple replicas require a shared limiter/cache or a different provider. No autocomplete or bulk crawl.
public sealed class PlaceSearchService(IHttpClientFactory clients, IOptions<PlaceSearchOptions> options,
    TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, (DateTimeOffset Expires, IReadOnlyList<PlaceSearchResponse> Items)> cache = [];
    private DateTimeOffset nextRequest;

    public async Task<IReadOnlyList<PlaceSearchResponse>> SearchAsync(string? query, CancellationToken ct)
    {
        var normalized = string.Join(' ', (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length is < 3 or > 200)
            throw new ValidationAppException(new Dictionary<string, string[]> { ["query"] = ["Nhập tên địa điểm công cộng từ 3 đến 200 ký tự."] });
        if (!options.Value.Enabled) throw Unavailable();
        if (!await gate.WaitAsync(0, ct))
            throw new TooManyRequestsException("Đang có yêu cầu tìm địa điểm. Vui lòng thử lại sau giây lát.");
        try
        {
            var key = normalized.ToLowerInvariant();
            if (cache.TryGetValue(key, out var hit) && hit.Expires > clock.GetUtcNow()) return hit.Items;
            var wait = nextRequest - clock.GetUtcNow();
            if (wait > TimeSpan.Zero) await Task.Delay(wait, clock, ct);
            ct.ThrowIfCancellationRequested();
            nextRequest = clock.GetUtcNow().AddMilliseconds(1100);
            var uri = options.Value.Endpoint + "?format=jsonv2&countrycodes=vn&accept-language=vi&limit=5&q=" + Uri.EscapeDataString(normalized);
            using var client = clients.CreateClient("VieGoPlaceSearch");
            using var response = await client.GetAsync(uri, ct);
            if (!response.IsSuccessStatusCode) throw Unavailable();
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            if (json.RootElement.ValueKind != JsonValueKind.Array) throw Unavailable();
            var items = new List<PlaceSearchResponse>();
            foreach (var row in json.RootElement.EnumerateArray().Take(5))
            {
                if (!row.TryGetProperty("lat", out var lat) || !row.TryGetProperty("lon", out var lon) ||
                    !decimal.TryParse(lat.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude) ||
                    !decimal.TryParse(lon.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
                    latitude is < -90 or > 90 || longitude is < -180 or > 180 ||
                    !row.TryGetProperty("display_name", out var display) || string.IsNullOrWhiteSpace(display.GetString()))
                    throw Unavailable();
                var full = display.GetString()!;
                var name = row.TryGetProperty("name", out var n) && !string.IsNullOrWhiteSpace(n.GetString()) ? n.GetString()! : full.Split(',')[0];
                var sourceId = row.TryGetProperty("osm_type", out var type) && row.TryGetProperty("osm_id", out var id)
                    ? $"osm:{type.GetString()}:{id}" : "";
                items.Add(new(sourceId, name[..Math.Min(name.Length, 200)], full[..Math.Min(full.Length, 500)], latitude, longitude));
            }
            foreach (var expired in cache.Where(x => x.Value.Expires <= clock.GetUtcNow()).Select(x => x.Key).ToArray()) cache.Remove(expired);
            if (cache.Count >= 256) cache.Remove(cache.Keys.First());
            cache[key] = (clock.GetUtcNow().AddMinutes(15), items.AsReadOnly());
            return cache[key].Items;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (JsonException) { throw Unavailable(); }
        catch (InvalidOperationException) { throw Unavailable(); }
        finally { gate.Release(); }
    }

    private static ServiceUnavailableAppException Unavailable() => new("Chưa kết nối được dịch vụ tìm địa điểm. Hãy thử lại hoặc chọn ghim thủ công.");
    public void Dispose() => gate.Dispose();
}
