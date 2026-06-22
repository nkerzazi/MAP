using MediaPlatform.Application.Admin;

namespace MediaPlatform.Application.Interfaces;

public interface IAdminService
{
    Task<IReadOnlyList<UserAdminItem>> ListUsersAsync(CancellationToken ct = default);
    Task UpdateUserAsync(Guid id, bool isActive, string? displayName, Guid actorId, CancellationToken ct = default);
    Task AssignRoleAsync(Guid id, string role, Guid actorId, CancellationToken ct = default);
    Task RemoveRoleAsync(Guid id, string role, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetConfigAsync(CancellationToken ct = default);
    Task SetConfigAsync(string key, string value, Guid actorId, CancellationToken ct = default);
}
