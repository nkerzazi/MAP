using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Admin;

public class AuditService : IAuditService
{
    private const int PageSize = 20;
    private readonly AppDbContext _db;
    public AuditService(AppDbContext db) => _db = db;

    public async Task LogAsync(Guid? actorId, string action, string entityType, string? entityId, string? metadata = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), ActorId = actorId, Action = action,
            EntityType = entityType, EntityId = entityId, Metadata = metadata
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditEntry>> ListAsync(int page, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        var query = _db.AuditLogs.OrderByDescending(a => a.OccurredAt)
            .Select(a => new AuditEntry(a.Id, a.ActorId, a.Action, a.EntityType, a.EntityId, a.OccurredAt));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        return new PagedResult<AuditEntry>(items, total, page, PageSize);
    }
}
