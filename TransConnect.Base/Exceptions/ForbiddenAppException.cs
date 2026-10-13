namespace TransConnect.Base.Exceptions;

public sealed class ForbiddenAppException(string message = "Bạn không có quyền thực hiện thao tác này.")
    : AppException(AppErrorCodes.Forbidden, message);
