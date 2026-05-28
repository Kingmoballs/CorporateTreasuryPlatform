using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Treasury.Application.Interfaces;
using Treasury.Domain.Entities;

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
                user.Role.Name)
        };

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