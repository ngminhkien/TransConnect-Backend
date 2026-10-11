using TransConnect.Base.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TransConnect.Common.Response;

namespace TransConnect.Common.Auth;

// Đọc permission thẳng từ claim trong JWT (đã có sẵn — xem TokenService), không query DB
// mỗi request. Đánh đổi: thu hồi permission ở DB không có hiệu lực ngay cho token đang sống,
// chỉ có hiệu lực từ lần refresh/login tiếp theo.
public class RequirePermissionAttribute(string permission) : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;

        // Trả thẳng ObjectResult thay vì UnauthorizedResult/ForbidResult: 2 loại đó cho body
        // rỗng, phá vỡ contract "mọi response đều là ApiResponse" mà client đang dựa vào.
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = Failure(
                StatusCodes.Status401Unauthorized,
                AppErrorCodes.Unauthorized,
                "Bạn cần đăng nhập để dùng chức năng này.");

            return Task.CompletedTask;
        }

        if (!user.HasClaim(AppClaimTypes.Permission, permission))
        {
            context.Result = Failure(
                StatusCodes.Status403Forbidden,
                AppErrorCodes.Forbidden,
                "Bạn không có quyền thực hiện thao tác này.");
        }

        return Task.CompletedTask;
    }

    private static ObjectResult Failure(int statusCode, string errorCode, string message) =>
        new(ApiResponse<object>.Failure(message, errorCode)) { StatusCode = statusCode };
}
