using Microsoft.Data.SqlClient;

namespace TransConnect.Host.Configuration;

// SqlClient can surface an acknowledged command cancellation as SqlException,
// not OperationCanceledException. Keep provider knowledge in the composition root.
public static class RequestCancellation
{
    public static bool IsClientCancellation(Exception exception, CancellationToken requestAborted)
    {
        if (!requestAborted.IsCancellationRequested) return false;
        if (exception is OperationCanceledException) return true;
        // Number 0 alone is ambiguous. Require the complete client attention shape;
        // never match a localized message or hide timeouts / accompanying SQL errors.
        return exception is SqlException sql && sql.Errors.Count > 0
            && sql.Errors.Cast<SqlError>().All(e => e.Number == 0 && e.State == 0 && e.Class == 11);
    }
}
