using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using MediaPlatform.Application.Auth;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Xunit;

namespace MediaPlatform.Tests;

public class JwtTokenGeneratorTests
{
    private static JwtTokenGenerator NewGen() => new(Options.Create(new JwtOptions
    {
        Key = "une-cle-de-test-suffisamment-longue-pour-hmac-sha256-0123456789",
        Issuer = "map", Audience = "map", ExpiryHours = 8
    }));

    [Fact]
    public void Generate_emits_expected_claims_and_expiry()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "a@map.ma", DisplayName = "Alice" };
        var (token, expiresAt) = NewGen().Generate(user, new[] { "Visiteur", "Editeur" });

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c => c.Type == "sub" && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == "a@map.ma");
        jwt.Claims.Should().Contain(c => c.Type == "name" && c.Value == "Alice");
        jwt.Claims.Where(c => c.Type == "role").Select(c => c.Value)
            .Should().BeEquivalentTo(new[] { "Visiteur", "Editeur" });
        jwt.Issuer.Should().Be("map");
        expiresAt.Should().BeCloseTo(DateTimeOffset.UtcNow.AddHours(8), TimeSpan.FromMinutes(1));
    }
}
