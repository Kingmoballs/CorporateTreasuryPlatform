using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Authentication;

public class JwtService : IJwtService
{
    private readonly JwtSettingsOptions _options;

    private readonly TimeProvider _timeProvider;

    public JwtService(
        IOptions<JwtSettingsOptions> options,
        TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public string GenerateToken(
        User user,
        OrganizationMembership membership,
        Guid authenticationSessionId)
    {
        var claims = new List<Claim>
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new Claim(
                ClaimTypes.Email,
                user.Email),

            new Claim(
                ClaimTypes.Role,
                membership.Role.Name),

            new Claim(
                JwtRegisteredClaimNames.Jti,
                Guid.NewGuid().ToString()),

            new Claim(
                CustomClaimTypes.OrganizationId,
                membership.OrganizationId
                    .ToString()),

            new Claim(
                CustomClaimTypes.OrganizationCode,
                membership.Organization.Code),

            new Claim(
                CustomClaimTypes
                    .OrganizationMembershipId,
                membership.Id.ToString()),

            new Claim(
                CustomClaimTypes
                    .AuthenticationSessionId,
                authenticationSessionId.ToString())
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                _options.SecretKey));

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            notBefore: _timeProvider
                .GetUtcNow()
                .UtcDateTime,
            expires: _timeProvider
                .GetUtcNow()
                .UtcDateTime
                .AddMinutes(
                    _options.ExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}
