using Microsoft.AspNetCore.Http;

namespace TransConnect.Common.Response;

// Dùng cho các chỗ ghi response nằm NGOÀI pipeline MVC (vd JwtBearerEvents), nơi không trả
// được ActionResult. Mục đích: mọi response lỗi đều cùng shape ApiResponse, client chỉ phải
// parse 1 định dạng duy nhất.
public static class ApiResponseWriter
{
    public static Task WriteFailureAsync(
        HttpContext httpContext,
        int statusCode,
        string errorCode,
        string message)
    {
        // Response đã bắt đầu gửi thì không ghi đè được nữa — bỏ qua để không nuốt mất
        // exception thật bằng 1 InvalidOperationException khó hiểu.
        if (httpContext.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        httpContext.Response.StatusCode = statusCode;
        return httpContext.Response.WriteAsJsonAsync(ApiResponse<object>.Failure(message, errorCode));
    }
}
