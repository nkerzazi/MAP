namespace MediaPlatform.Application.Auth;

public record AuthResponse(
    string Token, DateTimeOffset ExpiresAt, Guid UserId,
    string Email, string DisplayName, IReadOnlyList<string> Roles);
