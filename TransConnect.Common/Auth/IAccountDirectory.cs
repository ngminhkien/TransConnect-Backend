namespace TransConnect.Common.Auth;

// Read-only Identity boundary: callers never access its entities or DbContext.
public interface IAccountDirectory
{
    Task<AccountSummary?> FindActiveAsync(Guid accountId, CancellationToken cancellationToken = default);
}
