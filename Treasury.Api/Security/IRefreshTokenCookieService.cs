namespace Treasury.Api.Security;

public interface IRefreshTokenCookieService
{
    string GetRequiredToken(
        HttpRequest request,
        string? clientRequestHeader);

    void Append(
        HttpResponse response,
        string refreshToken,
        DateTime expiresAtUtc);

    void Delete(HttpResponse response);
}
