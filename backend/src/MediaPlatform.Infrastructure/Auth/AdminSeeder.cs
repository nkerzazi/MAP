using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Auth;

/// <summary>Crée un compte Admin au démarrage si absent (idempotent). Aucun identifiant en dur.</summary>
public class AdminSeeder
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    public AdminSeeder(AppDbContext db, IPasswordHasher<User> hasher) { _db = db; _hasher = hasher; }

    public async Task SeedAsync(string? email, string? password, string displayName = "Administrateur",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return; // rien à faire si non configuré

        var normalized = email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == normalized, ct))
            return; // idempotent

        var adminRole = await _db.Roles.SingleAsync(r => r.Name == "Admin", ct);
        var user = new User { Id = Guid.NewGuid(), Email = normalized, DisplayName = displayName, IsActive = true };
        user.PasswordHash = _hasher.HashPassword(user, password);
        user.Roles.Add(new UserRole { UserId = user.Id, RoleId = adminRole.Id });
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
    }
}
