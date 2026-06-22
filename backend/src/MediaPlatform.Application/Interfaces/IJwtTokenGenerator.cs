using MediaPlatform.Domain.Entities;

namespace MediaPlatform.Application.Interfaces;

public interface IJwtTokenGenerator
{
    (string Token, DateTimeOffset ExpiresAt) Generate(User user, IEnumerable<string> roles);
}
