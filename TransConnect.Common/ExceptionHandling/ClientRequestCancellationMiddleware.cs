using Microsoft.AspNetCore.Http;

namespace TransConnect.Common.ExceptionHandling;

// Place inside request logging so a disconnected client is not logged as a 500.
public sealed class ClientRequestCancellationMiddleware(RequestDelegate next,
    Func<Exception, CancellationToken, bool>? isProviderCancellation = null)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (context.RequestAborted.IsCancellationRequested
            && (exception is OperationCanceledException
                || isProviderCancellation?.Invoke(exception, context.RequestAborted) == true))
        {
            // The connection is gone: do not attempt to write an error envelope.
            if (!context.Response.HasStarted)
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
    }
}
