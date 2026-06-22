using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Catalog;

public class TagService : ITagService
{
    private readonly AppDbContext _db;
    public TagService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TagItem>> ListAsync(string? q, CancellationToken ct = default)
    {
        var query = _db.Tags.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var lowered = q.Trim().ToLower();
            query = query.Where(t => t.Name.ToLower().Contains(lowered));
        }
        return await query.OrderBy(t => t.Name)
            .Select(t => new TagItem(t.Id, t.Name))
            .ToListAsync(ct);
    }
}
