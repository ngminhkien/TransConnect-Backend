namespace TransConnect.Base.Exceptions;

public class ConflictException : AppException
{
    public ConflictException(string message) : base(AppErrorCodes.Conflict, message)
    {
    }
}
