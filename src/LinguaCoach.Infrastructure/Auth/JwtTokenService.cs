using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LinguaCoach.Application.Auth;
using LinguaCoach.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace LinguaCoach.Infrastructure.Auth;

public sealed class JwtTokenService : ITokenService
{
    private readonly string _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryHours;

    public JwtTokenService(IConfiguration configuration)
    {
        _signingKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is missing from configuration.");
        _issuer = configuration["Jwt:Issuer"] ?? "linguacoach";
        _audience = configuration["Jwt:Audience"] ?? "linguacoach";
        _expiryHours = int.TryParse(configuration["Jwt:ExpiryHours"], out var h) ? h : 24;
    }

    public string GenerateToken(Guid userId, string email, UserRole role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_expiryHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
