using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Catalog;

namespace MediaPlatform.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(Guid? actorId, string action, string entityType, string? entityId, string? metadata = null, CancellationToken ct = default);
    Task<PagedResult<AuditEntry>> ListAsync(int page, CancellationToken ct = default);
}
