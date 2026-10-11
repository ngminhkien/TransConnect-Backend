using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TransConnect.Base.Exceptions;

namespace TransConnect.Common.Response;

public static class ApiStatusCodeExtensions
{
    // Chỉ bổ sung body khi pipeline trả 4xx/5xx rỗng; không ghi đè response đã có.
    public static IApplicationBuilder UseApiStatusCodePages(this IApplicationBuilder app) =>
        app.UseStatusCodePages(context =>
        {
            var status = context.HttpContext.Response.StatusCode;
            var (code, message) = status switch
            {
                400 => (AppErrorCodes.ValidationError, "Dữ liệu không hợp lệ."),
                401 => (AppErrorCodes.Unauthorized, "Bạn cần đăng nhập để dùng chức năng này."),
                403 => (AppErrorCodes.Forbidden, "Bạn không có quyền thực hiện thao tác này."),
                404 => (AppErrorCodes.NotFound, "Không tìm thấy tài nguyên được yêu cầu."),
                405 => (AppErrorCodes.MethodNotAllowed, "Phương thức HTTP không được hỗ trợ tại đường dẫn này."),
                415 => (AppErrorCodes.UnsupportedMediaType, "Định dạng nội dung không được hỗ trợ. Vui lòng gửi JSON."),
                429 => (AppErrorCodes.TooManyRequests, "Bạn gửi quá nhiều yêu cầu. Vui lòng thử lại sau."),
                >= 500 => (AppErrorCodes.InternalError, "Đã xảy ra lỗi hệ thống."),
                _ => (AppErrorCodes.RequestFailed, "Không thể xử lý yêu cầu.")
            };
            return ApiResponseWriter.WriteFailureAsync(context.HttpContext, status, code, message);
        });
}
