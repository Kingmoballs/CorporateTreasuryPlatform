using System.Security.Cryptography;
using System.Text;

namespace Treasury.Infrastructure.Services;

internal static class UserInvitationTokenHelper
{
    private const int TokenByteCount = 32;

    public static string Generate()
    {
        var bytes =
            RandomNumberGenerator.GetBytes(
                TokenByteCount);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string Hash(string rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return string.Empty;
        }

        var hash =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    rawToken));

        return Convert.ToHexString(hash);
    }

    public static string BuildAcceptanceUrl(
        string baseUrl,
        string rawToken)
    {
        var separator =
            baseUrl.Contains(
                '?',
                StringComparison.Ordinal)
                ? "&"
                : "?";

        return baseUrl +
               separator +
               "token=" +
               Uri.EscapeDataString(rawToken);
    }
}
