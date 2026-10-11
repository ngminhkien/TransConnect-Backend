namespace TransConnect.Base.Exceptions;

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(AppErrorCodes.NotFound, message)
    {
    }

    public NotFoundException(string entityName, object key)
        : base(AppErrorCodes.NotFound, $"{entityName} với id '{key}' không tồn tại.")
    {
    }
}
