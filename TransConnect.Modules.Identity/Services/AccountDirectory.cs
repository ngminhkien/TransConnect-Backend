using Microsoft.EntityFrameworkCore;
using TransConnect.Common.Auth;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Services;

public sealed class AccountDirectory(IdentityDbContext db) : IAccountDirectory
{
    public Task<AccountSummary?> FindActiveAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().Where(u => u.Id == accountId && u.IsActive)
            .Select(u => new AccountSummary(u.Id, u.Email, u.AccountGroup, u.EmailVerifiedAt != null))
            .SingleOrDefaultAsync(cancellationToken);
}
