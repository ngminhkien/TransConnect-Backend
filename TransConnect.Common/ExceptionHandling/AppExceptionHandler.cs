using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TransConnect.Base.Exceptions;
using TransConnect.Common.Response;

namespace TransConnect.Common.ExceptionHandling;

// Chỗ duy nhất map exception -> HTTP response. Controller không tự try/catch,
// xem quy tắc trong docs/coding-conventions.md.
public class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, errorCode, message) = Map(exception);

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception");
        }

        httpContext.Response.StatusCode = statusCode;

        var errors = (exception as ValidationAppException)?.Errors;
        var response = ApiResponse<object>.Failure(message, errorCode, errors);
        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }

    private static (int StatusCode, string ErrorCode, string Message) Map(Exception exception) => exception switch
    {
        NotFoundException e => (StatusCodes.Status404NotFound, e.ErrorCode, e.Message),
        ValidationAppException e => (StatusCodes.Status400BadRequest, e.ErrorCode, e.Message),
        ConflictException e => (StatusCodes.Status409Conflict, e.ErrorCode, e.Message),
        UnauthorizedAppException e => (StatusCodes.Status401Unauthorized, e.ErrorCode, e.Message),
        ForbiddenAppException e => (StatusCodes.Status403Forbidden, e.ErrorCode, e.Message),
        TooManyRequestsException e => (StatusCodes.Status429TooManyRequests, e.ErrorCode, e.Message),
        _ => (StatusCodes.Status500InternalServerError, AppErrorCodes.InternalError, "Đã xảy ra lỗi hệ thống.")
    };
}
