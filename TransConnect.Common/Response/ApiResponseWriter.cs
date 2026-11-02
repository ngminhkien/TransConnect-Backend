using Microsoft.AspNetCore.Http;

namespace TransConnect.Common.Response;

// Dùng cho các chỗ ghi response nằm NGOÀI pipeline MVC (vd JwtBearerEvents), nơi không trả
// được ActionResult. Mục đích: mọi response lỗi đều cùng shape ApiResponse, client chỉ phải
// parse 1 định dạng duy nhất.
public static class ApiResponseWriter
{
    public static async Task WriteFailureAsync(
        HttpContext httpContext,
        int statusCode,
        string errorCode,
        string message)
    {
        // Response đã bắt đầu gửi thì không ghi đè được nữa — bỏ qua để không nuốt mất
        // exception thật bằng 1 InvalidOperationException khó hiểu.
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        if (httpContext.RequestAborted.IsCancellationRequested)
        {
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return;
        }

        httpContext.Response.StatusCode = statusCode;
        try
        {
            await httpContext.Response.WriteAsJsonAsync(ApiResponse<object>.Failure(message, errorCode),
                httpContext.RequestAborted);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            if (!httpContext.Response.HasStarted)
                httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
    }
}
