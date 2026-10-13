using System.Security.Cryptography;
using System.Text;

namespace TransConnect.Modules.Operator.Services;

internal static class InvitationTokens
{
    public static string Create() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
