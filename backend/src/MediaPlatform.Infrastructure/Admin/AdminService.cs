using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Admin;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    public AdminService(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<IReadOnlyList<UserAdminItem>> ListUsersAsync(CancellationToken ct = default) =>
        await _db.Users.Include(u => u.Roles).ThenInclude(ur => ur.Role)
            .Select(u => new UserAdminItem(u.Id, u.Email, u.DisplayName, u.IsActive,
                u.Roles.Select(ur => ur.Role!.Name).ToList()))
            .ToListAsync(ct);

    public async Task UpdateUserAsync(Guid id, bool isActive, string? displayName, Guid actorId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new UserNotFoundException();
        user.IsActive = isActive;
        if (!string.IsNullOrWhiteSpace(displayName)) user.DisplayName = displayName.Trim();
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "user.update", "User", id.ToString(), $"isActive={isActive}", ct);
    }

    public async Task AssignRoleAsync(Guid id, string role, Guid actorId, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new UserNotFoundException();
        var r = await _db.Roles.FirstOrDefaultAsync(x => x.Name == role, ct) ?? throw new UnknownRoleException(role);
        if (user.Roles.All(ur => ur.RoleId != r.Id))
        {
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = r.Id });
            await _db.SaveChangesAsync(ct);
        }
        await _audit.LogAsync(actorId, "role.assign", "User", id.ToString(), role, ct);
    }

    public async Task RemoveRoleAsync(Guid id, string role, Guid actorId, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new UserNotFoundException();
        var r = await _db.Roles.FirstOrDefaultAsync(x => x.Name == role, ct) ?? throw new UnknownRoleException(role);
        var link = user.Roles.FirstOrDefault(ur => ur.RoleId == r.Id);
        if (link is not null)
        {
            user.Roles.Remove(link);
            await _db.SaveChangesAsync(ct);
        }
        await _audit.LogAsync(actorId, "role.remove", "User", id.ToString(), role, ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetConfigAsync(CancellationToken ct = default) =>
        await _db.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    public async Task SetConfigAsync(string key, string value, Guid actorId, CancellationToken ct = default)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null) _db.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
        else { setting.Value = value; setting.UpdatedAt = DateTimeOffset.UtcNow; }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "config.set", "Config", key, value, ct);
    }
}
