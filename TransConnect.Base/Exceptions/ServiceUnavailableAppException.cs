namespace TransConnect.Base.Exceptions;

public sealed class ServiceUnavailableAppException(string message)
    : AppException("SERVICE_UNAVAILABLE", message);
