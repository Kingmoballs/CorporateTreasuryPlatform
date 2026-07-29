using Microsoft.AspNetCore.Http;

namespace Treasury.Api.Configuration;

public class RefreshTokenCookieOptions
{
    public const string SectionName =
        "RefreshTokenCookie";

    public string Name { get; set; } =
        "Treasury.RefreshToken";

    public string Path { get; set; } =
        "/api/v1/auth";

    public bool Secure { get; set; } = true;

    public SameSiteMode SameSite { get; set; } =
        SameSiteMode.Strict;
}
