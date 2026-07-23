using Microsoft.AspNetCore.DataProtection;
using Treasury.Application.Interfaces;

namespace Treasury.Infrastructure.Authentication;

public class MfaSecretProtector
    : IMfaSecretProtector
{
    private readonly IDataProtector _protector;

    public MfaSecretProtector(
        IDataProtectionProvider provider)
    {
        _protector =
            provider.CreateProtector(
                "Treasury.Mfa.TotpSecret.v1");
    }

    public string Protect(string secret)
    {
        return _protector.Protect(secret);
    }

    public string Unprotect(
        string protectedSecret)
    {
        return _protector.Unprotect(
            protectedSecret);
    }
}
