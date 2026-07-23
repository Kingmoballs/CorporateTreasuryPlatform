using Treasury.Infrastructure.Authentication;

namespace Treasury.Tests.Authentication;

public class TotpServiceTests
{
    [Fact]
    public void Verify_MatchesRfc6238Sha1Vector()
    {
        var service = new TotpService();

        /*
         * RFC 6238 uses this Base32 secret at Unix time 59.
         * Its eight-digit SHA-1 result is 94287082; the
         * corresponding six-digit value is 287082.
         */
        var valid =
            service.Verify(
                "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",
                "287082",
                DateTime.UnixEpoch
                    .AddSeconds(59));

        Assert.True(valid);
        Assert.False(
            service.Verify(
                "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",
                "287083",
                DateTime.UnixEpoch
                    .AddSeconds(59)));
    }

    [Fact]
    public void GenerateSecret_ProducesValidUniqueKeys()
    {
        var service = new TotpService();

        var first = service.GenerateSecret();
        var second = service.GenerateSecret();

        Assert.Equal(32, first.Length);
        Assert.Equal(32, second.Length);
        Assert.NotEqual(first, second);
        Assert.Matches(
            "^[A-Z2-7]+$",
            first);
    }

    [Fact]
    public void ProvisioningUri_EncodesIssuerAndAccount()
    {
        var service = new TotpService();

        var uri =
            service.BuildProvisioningUri(
                "Treasury & Finance",
                "ada+admin@example.com",
                "ABCDEF234567");

        Assert.StartsWith(
            "otpauth://totp/",
            uri,
            StringComparison.Ordinal);
        Assert.Contains(
            "Treasury%20%26%20Finance",
            uri,
            StringComparison.Ordinal);
        Assert.Contains(
            "ada%2Badmin%40example.com",
            uri,
            StringComparison.Ordinal);
        Assert.Contains(
            "secret=ABCDEF234567",
            uri,
            StringComparison.Ordinal);
    }
}
