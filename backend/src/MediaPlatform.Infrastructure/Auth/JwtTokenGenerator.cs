using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MediaPlatform.Application.Auth;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace MediaPlatform.Infrastructure.Auth;

/// <summary>Émet un JWT HMAC-SHA256 avec claims courts (sub/email/name/role).</summary>
public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _opts;
    public JwtTokenGenerator(IOptions<JwtOptions> opts) => _opts = opts.Value;

    public (string Token, DateTimeOffset ExpiresAt) Generate(User user, IEnumerable<string> roles)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(_opts.ExpiryHours);
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email),
            new("name", user.DisplayName),
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _opts.Issuer, audience: _opts.Audience, claims: claims,
            expires: expiresAt.UtcDateTime, signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
