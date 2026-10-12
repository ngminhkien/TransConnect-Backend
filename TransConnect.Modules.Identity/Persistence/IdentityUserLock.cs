using Microsoft.EntityFrameworkCore;

namespace TransConnect.Modules.Identity.Persistence;

internal static class IdentityUserLock
{
    // Caller owns the transaction. No-op UPDATE holds a database write lock before
    // reading OTPs or claiming refresh tokens. Consistent User -> token lock order
    // prevents reset/refresh from locking the same rows in opposite orders.
    public static Task<int> AcquireAsync(IdentityDbContext dbContext, Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.Where(u => u.Id == userId && u.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.UpdatedAt, u => u.UpdatedAt), cancellationToken);
}
