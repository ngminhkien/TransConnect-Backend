using Microsoft.EntityFrameworkCore;
using TransConnect.Common.Auth;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Services;

public sealed class AccountDisplayDirectory(IdentityDbContext db) : IAccountDisplayDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        await db.Users.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
}
