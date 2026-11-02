namespace TransConnect.Common.Auth;

// Existence includes inactive and soft-deleted accounts; this is not an authorization check.
public interface IAccountReferenceDirectory
{
    Task<IReadOnlySet<Guid>> FindMissingAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken ct);
}
