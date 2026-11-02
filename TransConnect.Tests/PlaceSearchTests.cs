using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using TransConnect.Base.Exceptions;
using TransConnect.Modules.Locations.Services;
using Xunit;

namespace TransConnect.Tests;

public sealed class PlaceSearchTests
{
    private const string Place = "[{\"osm_type\":\"node\",\"osm_id\":42,\"name\":\"Bến xe Miền Đông\",\"display_name\":\"Bến xe Miền Đông, Việt Nam\",\"lat\":\"10.879\",\"lon\":\"106.814\"}]";

    [Fact]
    public async Task Search_normalizes_caches_and_returns_provider_coordinates_without_VieGo_identity()
    {
        using var handler = new Handler(_ => Task.FromResult(Json(Place)));
        using var service = Create(handler);
        var first = await service.SearchAsync("  Bến   xe Miền Đông  ", default);
        var second = await service.SearchAsync("bến xe miền đông", default);
        Assert.Single(handler.Paths);
        Assert.Same(first, second);
        Assert.Equal(10.879m, first[0].Latitude);
        Assert.Equal(106.814m, first[0].Longitude);
        Assert.Equal("osm:node:42", first[0].SourceId);
        Assert.Contains("countrycodes=vn", handler.Paths[0]);
        Assert.DoesNotContain("account", handler.Paths[0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public async Task Short_query_does_not_call_provider(string query)
    {
        using var handler = new Handler(_ => Task.FromResult(Json(Place)));
        using var service = Create(handler);
        await Assert.ThrowsAsync<ValidationAppException>(() => service.SearchAsync(query, default));
        Assert.Empty(handler.Paths);
    }

    [Theory]
    [InlineData("[]", false)]
    [InlineData("{}", true)]
    [InlineData("[{\"display_name\":\"Point\",\"lat\":\"91\",\"lon\":\"106\"}]", true)]
    [InlineData("not json", true)]
    public async Task Empty_is_not_malformed_response(string body, bool invalid)
    {
        using var handler = new Handler(_ => Task.FromResult(Json(body)));
        using var service = Create(handler);
        if (invalid) await Assert.ThrowsAsync<ServiceUnavailableAppException>(() => service.SearchAsync("Bến xe", default));
        else Assert.Empty(await service.SearchAsync("Bến xe", default));
    }

    [Fact]
    public async Task Concurrent_miss_is_rejected_and_client_cancellation_releases_gate()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new Handler(async token => { started.SetResult(); await Task.Delay(Timeout.Infinite, token); return Json(Place); });
        using var service = Create(handler);
        using var cancellation = new CancellationTokenSource();
        var first = service.SearchAsync("Bến xe", cancellation.Token);
        await started.Task;
        await Assert.ThrowsAsync<TooManyRequestsException>(() => service.SearchAsync("Nhà ga", default));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
    }

    [Fact]
    public async Task All_users_share_outbound_spacing()
    {
        using var handler = new Handler(_ => Task.FromResult(Json(Place)));
        using var service = Create(handler);
        await service.SearchAsync("Bến xe", default);
        await service.SearchAsync("Nhà ga", default);
        Assert.True(handler.Times[1] - handler.Times[0] >= Stopwatch.Frequency, "Outbound calls must be at least one second apart.");
    }

    [Fact]
    public async Task Provider_outage_is_unavailable_not_empty()
    {
        using var handler = new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)));
        using var service = Create(handler);
        await Assert.ThrowsAsync<ServiceUnavailableAppException>(() => service.SearchAsync("Bến xe", default));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static PlaceSearchService Create(Handler handler) => new(new Factory(handler), Options.Create(new PlaceSearchOptions()), TimeProvider.System);
    private sealed class Factory(Handler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
    private sealed class Handler(Func<CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public List<long> Times { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.PathAndQuery);
            Times.Add(Stopwatch.GetTimestamp());
            return respond(cancellationToken);
        }
    }
}
