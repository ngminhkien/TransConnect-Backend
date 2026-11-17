using Microsoft.AspNetCore.Http;
using Serilog.Events;

namespace TransConnect.Host.Configuration;

// Request logging đứng NGOÀI UseExceptionHandler nên thấy đúng mã client nhận được
// (401/403/404... của exception nghiệp vụ không còn bị ghi thành 500). Stack trace của lỗi
// 500 thật do AppExceptionHandler ghi; dòng request log chỉ ghi mức độ theo status.
public static class RequestLogLevel
{
    public static LogEventLevel Get(HttpContext context, double elapsedMilliseconds, Exception? exception)
    {
        if (exception is not null) return LogEventLevel.Error;
        var status = context.Response.StatusCode;
        if (status >= StatusCodes.Status500InternalServerError) return LogEventLevel.Error;
        // Client tự hủy/ngắt kết nối không phải lỗi phía server.
        if (status == StatusCodes.Status499ClientClosedRequest) return LogEventLevel.Information;
        if (status >= StatusCodes.Status400BadRequest) return LogEventLevel.Warning;
        return LogEventLevel.Information;
    }
}
