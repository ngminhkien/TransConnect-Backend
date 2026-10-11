using TransConnect.Modules.Identity.Entities;

namespace TransConnect.Modules.Identity.Services;

public record AccessTokenResult(string AccessToken, string JwtId, DateTime ExpiresAt);

public interface ITokenService
{
    AccessTokenResult GenerateAccessToken(User user, IReadOnlyList<string> roles, IReadOnlyList<string> permissions);
    string GenerateRefreshToken();
}
