namespace Treasury.Application.Interfaces;

public interface IMfaSecretProtector
{
    string Protect(string secret);

    string Unprotect(string protectedSecret);
}
