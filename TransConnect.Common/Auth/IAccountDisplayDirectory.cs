namespace TransConnect.Common.Auth;

public interface IAccountDisplayDirectory
{
    Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
