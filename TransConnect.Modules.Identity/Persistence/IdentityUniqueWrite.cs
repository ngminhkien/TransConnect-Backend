using Microsoft.EntityFrameworkCore;
using TransConnect.Base.Exceptions;

namespace TransConnect.Modules.Identity.Persistence;

internal static class IdentityUniqueWrite
{
    // A preflight AnyAsync alone races with other requests. The unique index decides;
    // after a failed write, report 409 only if the conflicting active key exists in DB.
    public static async Task SaveAsync(IdentityDbContext dbContext,
        Func<CancellationToken, Task<bool>> conflictExists, string message, CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await conflictExists(cancellationToken)) throw new ConflictException(message);
            throw;
        }
    }
}
