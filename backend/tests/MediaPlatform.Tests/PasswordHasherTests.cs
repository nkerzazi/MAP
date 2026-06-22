using FluentAssertions;
using MediaPlatform.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace MediaPlatform.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_is_not_plaintext_and_verifies()
    {
        var hasher = new PasswordHasher<User>();
        var user = new User();
        var hash = hasher.HashPassword(user, "S3cret!");

        hash.Should().NotBe("S3cret!");
        hasher.VerifyHashedPassword(user, hash, "S3cret!").Should().Be(PasswordVerificationResult.Success);
        hasher.VerifyHashedPassword(user, hash, "mauvais").Should().Be(PasswordVerificationResult.Failed);
    }
}
