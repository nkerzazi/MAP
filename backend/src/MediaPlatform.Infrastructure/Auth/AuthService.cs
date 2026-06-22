using MediaPlatform.Application.Auth;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IJwtTokenGenerator _jwt;

    public AuthService(AppDbContext db, IPasswordHasher<User> hasher, IJwtTokenGenerator jwt)
    {
        _db = db; _hasher = hasher; _jwt = jwt;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new EmailAlreadyUsedException(email);

        var visiteur = await _db.Roles.SingleAsync(r => r.Name == "Visiteur", ct);
        var user = new User
        {
            Id = Guid.NewGuid(), Email = email, DisplayName = request.DisplayName, IsActive = true
        };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        user.Roles.Add(new UserRole { UserId = user.Id, RoleId = visiteur.Id });
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return BuildResponse(user, new[] { "Visiteur" });
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(u => u.Roles).ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !user.IsActive ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            throw new InvalidCredentialsException();

        var roles = user.Roles.Select(ur => ur.Role!.Name).ToList();
        return BuildResponse(user, roles);
    }

    private AuthResponse BuildResponse(User user, IReadOnlyList<string> roles)
    {
        var (token, expiresAt) = _jwt.Generate(user, roles);
        return new AuthResponse(token, expiresAt, user.Id, user.Email, user.DisplayName, roles);
    }
}
