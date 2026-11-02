namespace TransConnect.Common.Transport;

public sealed record ProfileDocumentScope(Guid ActorAccountId, Guid? OrganizationId);

// Operator owns relationships/permissions. Compliance owns the synthetic files and versions.
public interface IProfileDocumentAccess
{
    Task<ProfileDocumentScope> AuthorizeAsync(string subjectType, Guid subjectId, bool write, CancellationToken ct);
}
