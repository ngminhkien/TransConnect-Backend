namespace TransConnect.Common.Response;

public class ApiResponse<T>
{
    public bool Succeeded { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public T? Data { get; init; }
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }

    public static ApiResponse<T> Success(T data, string? message = null) => new()
    {
        Succeeded = true,
        Data = data,
        Message = message
    };

    public static ApiResponse<T> Failure(
        string message,
        string? errorCode = null,
        IReadOnlyDictionary<string, string[]>? errors = null) => new()
    {
        Succeeded = false,
        Message = message,
        ErrorCode = errorCode,
        Errors = errors
    };
}
