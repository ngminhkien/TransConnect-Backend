using System.Security.Cryptography;
using System.Text;

namespace TransConnect.Modules.Identity.Services;

// Refresh tokens contain 512 random bits; a deterministic digest supports indexed lookup.
// The prefix distinguishes migrated digests from legacy Base64 tokens.
internal static class RefreshTokenHash
{
    public static string Compute(string token) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
