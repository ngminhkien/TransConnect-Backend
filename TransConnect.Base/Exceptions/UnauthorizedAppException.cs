namespace TransConnect.Base.Exceptions;

public class UnauthorizedAppException : AppException
{
    public UnauthorizedAppException(string message) : base(AppErrorCodes.Unauthorized, message)
    {
    }
}
