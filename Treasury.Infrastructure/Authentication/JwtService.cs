using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;
using Treasury.Shared.Constants;

namespace Treasury.Infrastructure.Authentication;

public class JwtService : IJwtService
{
    private readonly IConfiguration _configuration;

    public JwtService(
        IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(User user)
    {
        var secretKey = _configuration[
            "JwtSettings:SecretKey"];

        var issuer = _configuration[
            "JwtSettings:Issuer"];

        var audience = _configuration[
            "JwtSettings:Audience"];

        var expiryMinutes = Convert.ToInt32(
            _configuration[
                "JwtSettings:ExpiryMinutes"]);

        var membership =
            user.OrganizationMemberships
                .Where(item =>
                    item.IsActive &&
                    item.Organization.IsActive)
                .OrderByDescending(item =>
                    item.IsDefault)
                .ThenBy(item =>
                    item.JoinedAtUtc)
                .FirstOrDefault();

        var roleName =
            membership?.Role.Name ??
            user.Role.Name;

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
                roleName)
        };

        if (membership != null)
        {
            claims.Add(
                new Claim(
                    CustomClaimTypes.OrganizationId,
                    membership.OrganizationId
                        .ToString()));

            claims.Add(
                new Claim(
                    CustomClaimTypes
                        .OrganizationCode,
                    membership.Organization.Code));

            claims.Add(
                new Claim(
                    CustomClaimTypes
                        .OrganizationMembershipId,
                    membership.Id.ToString()));
        }

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secretKey!));

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(
                expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}
