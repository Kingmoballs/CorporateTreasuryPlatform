using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Treasury.Application.Interfaces;

namespace Treasury.Infrastructure.Authentication;

public class TotpService : ITotpService
{
    private const int SecretByteCount = 20;
    private const long TimeStepSeconds = 30;
    private const int VerificationWindow = 1;

    private const string Base32Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret()
    {
        return EncodeBase32(
            RandomNumberGenerator.GetBytes(
                SecretByteCount));
    }

    public bool Verify(
        string secret,
        string code,
        DateTime nowUtc)
    {
        if (code.Length != 6 ||
            !code.All(char.IsAsciiDigit))
        {
            return false;
        }

        byte[] secretBytes;

        try
        {
            secretBytes = DecodeBase32(secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var unixSeconds =
            new DateTimeOffset(
                DateTime.SpecifyKind(
                    nowUtc,
                    DateTimeKind.Utc))
                .ToUnixTimeSeconds();

        var currentStep =
            unixSeconds / TimeStepSeconds;

        var suppliedCode =
            Encoding.ASCII.GetBytes(code);

        for (var offset = -VerificationWindow;
             offset <= VerificationWindow;
             offset++)
        {
            var expectedCode =
                Encoding.ASCII.GetBytes(
                    GenerateCode(
                        secretBytes,
                        currentStep + offset));

            if (CryptographicOperations
                .FixedTimeEquals(
                    suppliedCode,
                    expectedCode))
            {
                return true;
            }
        }

        return false;
    }

    public string BuildProvisioningUri(
        string issuer,
        string accountName,
        string secret)
    {
        var encodedIssuer =
            Uri.EscapeDataString(issuer);

        var encodedAccount =
            Uri.EscapeDataString(accountName);

        return "otpauth://totp/" +
            encodedIssuer +
            ":" +
            encodedAccount +
            "?secret=" +
            Uri.EscapeDataString(secret) +
            "&issuer=" +
            encodedIssuer +
            "&algorithm=SHA1&digits=6&period=30";
    }

    private static string GenerateCode(
        byte[] secret,
        long timeStep)
    {
        Span<byte> counter = stackalloc byte[8];

        BinaryPrimitives.WriteInt64BigEndian(
            counter,
            timeStep);

        using var hmac =
            new HMACSHA1(secret);

        var hash =
            hmac.ComputeHash(
                counter.ToArray());

        var offset =
            hash[^1] & 0x0F;

        var binaryCode =
            ((hash[offset] & 0x7F) << 24) |
            ((hash[offset + 1] & 0xFF) << 16) |
            ((hash[offset + 2] & 0xFF) << 8) |
            (hash[offset + 3] & 0xFF);

        return (binaryCode % 1_000_000)
            .ToString("D6");
    }

    private static string EncodeBase32(
        byte[] bytes)
    {
        var output = new StringBuilder();
        var buffer = 0;
        var bitsInBuffer = 0;

        foreach (var value in bytes)
        {
            buffer =
                (buffer << 8) | value;

            bitsInBuffer += 8;

            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;

                output.Append(
                    Base32Alphabet[
                        (buffer >>
                            bitsInBuffer) &
                        0x1F]);
            }
        }

        if (bitsInBuffer > 0)
        {
            output.Append(
                Base32Alphabet[
                    (buffer <<
                        (5 - bitsInBuffer)) &
                    0x1F]);
        }

        return output.ToString();
    }

    private static byte[] DecodeBase32(
        string value)
    {
        var normalized =
            value
                .Trim()
                .Replace(" ", string.Empty)
                .TrimEnd('=')
                .ToUpperInvariant();

        var output = new List<byte>();
        var buffer = 0;
        var bitsInBuffer = 0;

        foreach (var character in normalized)
        {
            var index =
                Base32Alphabet.IndexOf(
                    character);

            if (index < 0)
            {
                throw new FormatException(
                    "Invalid Base32 value.");
            }

            buffer =
                (buffer << 5) | index;

            bitsInBuffer += 5;

            if (bitsInBuffer < 8)
            {
                continue;
            }

            bitsInBuffer -= 8;

            output.Add(
                (byte)(
                    (buffer >>
                        bitsInBuffer) &
                    0xFF));
        }

        return output.ToArray();
    }
}
