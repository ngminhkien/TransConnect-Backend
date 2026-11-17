using Microsoft.AspNetCore.Http;
using Serilog.Events;
using TransConnect.Host.Configuration;
using Xunit;

namespace TransConnect.Tests;

public class RequestLogLevelTests
{
    private static LogEventLevel LevelFor(int status, Exception? exception = null)
    {
        var context = new DefaultHttpContext();
        context.Response.StatusCode = status;
        return RequestLogLevel.Get(context, 1, exception);
    }

    [Theory]
    [InlineData(200, LogEventLevel.Information)]
    [InlineData(204, LogEventLevel.Information)]
    [InlineData(400, LogEventLevel.Warning)]
    [InlineData(401, LogEventLevel.Warning)]
    [InlineData(403, LogEventLevel.Warning)]
    [InlineData(404, LogEventLevel.Warning)]
    [InlineData(409, LogEventLevel.Warning)]
    [InlineData(429, LogEventLevel.Warning)]
    [InlineData(499, LogEventLevel.Information)]
    [InlineData(500, LogEventLevel.Error)]
    [InlineData(503, LogEventLevel.Error)]
    public void TC_LOG_001_Request_log_level_follows_final_status(int status, LogEventLevel expected)
        => Assert.Equal(expected, LevelFor(status));

    [Fact]
    public void TC_LOG_002_Exception_escaping_the_pipeline_is_always_error()
        => Assert.Equal(LogEventLevel.Error, LevelFor(200, new InvalidOperationException()));
}
