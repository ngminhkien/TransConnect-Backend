namespace TransConnect.Base.Exceptions;

// Dùng khi người dùng gọi 1 thao tác quá nhiều lần trong khoảng thời gian cho phép
// (vd xin mã OTP liên tục). Map sang HTTP 429 trong AppExceptionHandler.
public class TooManyRequestsException : AppException
{
    public TooManyRequestsException(string message) : base(AppErrorCodes.TooManyRequests, message)
    {
    }
}
