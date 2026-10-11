namespace TransConnect.Base.Exceptions;

// Đặt tên "ValidationAppException" (không phải "ValidationException") để tránh đụng
// System.ComponentModel.DataAnnotations.ValidationException khi cả 2 namespace cùng using.
public class ValidationAppException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationAppException(string message) : base(AppErrorCodes.ValidationError, message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationAppException(IReadOnlyDictionary<string, string[]> errors)
        : base(AppErrorCodes.ValidationError, "Dữ liệu không hợp lệ.")
    {
        Errors = errors;
    }
}
