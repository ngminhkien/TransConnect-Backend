using Microsoft.EntityFrameworkCore;
using TransConnect.Common.Auth;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Services;

public sealed class AccountReferenceDirectory(IdentityDbContext db) : IAccountReferenceDirectory
{
    public async Task<IReadOnlySet<Guid>> FindMissingAsync(IReadOnlyCollection<Guid> accountIds, CancellationToken ct)
    {
        var existing = new HashSet<Guid>();
        foreach (var batch in accountIds.Distinct().Chunk(200))
            existing.UnionWith(await db.Users.IgnoreQueryFilters().AsNoTracking()
                .Where(x => batch.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct));
        return accountIds.Where(x => !existing.Contains(x)).ToHashSet();
    }
}
