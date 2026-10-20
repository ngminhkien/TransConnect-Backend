using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TransConnect.Common.Auth;
using TransConnect.Modules.Identity.Persistence;

namespace TransConnect.Modules.Identity.Services;

// Replaces authorization claims on every authenticated request, including old JWTs.
public sealed class LiveAccountClaims(IdentityDbContext db, IPermissionService permissions)
{
    public async Task<bool> RefreshAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return false;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (user is null || principal.Identity is not ClaimsIdentity identity) return false;
        foreach (var claim in identity.FindAll(ClaimTypes.Role).Concat(identity.FindAll(AppClaimTypes.Permission)).ToArray())
            identity.RemoveClaim(claim);
        foreach (var role in await permissions.GetRoleNamesAsync(id, ct))
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        foreach (var permission in await permissions.GetPermissionCodesAsync(id, ct))
            identity.AddClaim(new Claim(AppClaimTypes.Permission, permission));
        return true;
    }
}
