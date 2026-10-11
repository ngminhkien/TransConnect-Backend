namespace TransConnect.Base.Exceptions;

// Mã ổn định cho client; thông báo hiển thị bằng tiếng Việt. Xem docs/api-conventions.md.
public static class AppErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string TooManyRequests = "TOO_MANY_REQUESTS";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string RequestFailed = "REQUEST_FAILED";
    public const string InternalError = "INTERNAL_ERROR";
}
