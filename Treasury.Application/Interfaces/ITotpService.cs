namespace Treasury.Application.Interfaces;

public interface ITotpService
{
    string GenerateSecret();

    bool Verify(
        string secret,
        string code,
        DateTime nowUtc);

    string BuildProvisioningUri(
        string issuer,
        string accountName,
        string secret);
}
