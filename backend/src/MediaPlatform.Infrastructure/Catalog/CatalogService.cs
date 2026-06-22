using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Catalog;

public class CatalogService : ICatalogService
{
    private readonly AppDbContext _db;
    public CatalogService(AppDbContext db) => _db = db;

    public async Task<VideoDetail> UpdateAsync(Guid id, UpdateVideoRequest request, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var video = await _db.Videos.Include(v => v.Tags)
            .FirstOrDefaultAsync(v => v.Id == id, ct) ?? throw new VideoNotFoundException();
        EnsureOwner(video, userId, isAdmin);

        if (request.CategoryId is { } cat && !await _db.Categories.AnyAsync(c => c.Id == cat, ct))
            throw new CategoryNotFoundException(cat);

        video.Title = request.Title;
        video.Description = request.Description;
        video.CategoryId = request.CategoryId;
        video.UpdatedAt = DateTimeOffset.UtcNow;

        await SetTagsAsync(video, request.Tags, ct);
        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(id, userId, isAdmin, ct);
    }

    public async Task PublishAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var video = await LoadOwned(id, userId, isAdmin, ct);
        if (video.Status is not (VideoStatus.Ready or VideoStatus.Archived))
            throw new InvalidVideoStateException($"Publication impossible depuis l'état {video.Status}.");
        video.Status = VideoStatus.Published;
        video.PublishedAt = DateTimeOffset.UtcNow;
        video.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var video = await LoadOwned(id, userId, isAdmin, ct);
        if (video.Status != VideoStatus.Published)
            throw new InvalidVideoStateException($"Archivage impossible depuis l'état {video.Status}.");
        video.Status = VideoStatus.Archived;
        video.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<VideoListItem>> GetMineAsync(Guid userId, int page, CancellationToken ct = default)
    {
        var query = _db.Videos.Where(v => v.OwnerId == userId).OrderByDescending(v => v.UpdatedAt);
        return await PageAsync(query, page, 20, ct);
    }

    public async Task<PagedResult<VideoListItem>> SearchPublishedAsync(CatalogQuery query, CancellationToken ct = default)
    {
        var q = _db.Videos.Where(v => v.Status == VideoStatus.Published);

        if (query.CategoryId is { } cat) q = q.Where(v => v.CategoryId == cat);
        if (!string.IsNullOrWhiteSpace(query.Tag)) q = q.Where(v => v.Tags.Any(t => t.Tag!.Name == query.Tag));

        IOrderedQueryable<Video> ordered;
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            // EF.Functions.* doit rester inline dans l'expression pour être traduit en SQL.
            var q2 = query.Q;
            ordered = q.Where(v => v.SearchVector.Matches(EF.Functions.WebSearchToTsQuery("french", q2)))
                       .OrderByDescending(v => v.SearchVector.Rank(EF.Functions.WebSearchToTsQuery("french", q2)));
        }
        else
        {
            ordered = q.OrderByDescending(v => v.PublishedAt);
        }

        return await PageAsync(ordered, query.Page, query.PageSize <= 0 ? 20 : query.PageSize, ct);
    }

    public async Task<VideoDetail> GetDetailAsync(Guid id, Guid? userId, bool isAdmin, CancellationToken ct = default)
    {
        var video = await _db.Videos
            .Include(v => v.Category)
            .Include(v => v.Renditions)
            .Include(v => v.Tags).ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(v => v.Id == id, ct) ?? throw new VideoNotFoundException();

        var visible = video.Status == VideoStatus.Published || (userId is { } u && (isAdmin || video.OwnerId == u));
        if (!visible) throw new VideoNotFoundException();

        return new VideoDetail(video.Id, video.Title, video.Description, video.Slug, video.Status.ToString(),
            video.Category?.Name, video.DurationSeconds, video.PublishedAt,
            video.Tags.Select(t => t.Tag!.Name).ToList(),
            video.Renditions.Select(r => new RenditionInfo(r.Resolution, r.Bitrate, r.ManifestKey)).ToList());
    }

    // --- helpers ---
    private async Task<Video> LoadOwned(Guid id, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var video = await _db.Videos.FirstOrDefaultAsync(v => v.Id == id, ct) ?? throw new VideoNotFoundException();
        EnsureOwner(video, userId, isAdmin);
        return video;
    }

    private static void EnsureOwner(Video video, Guid userId, bool isAdmin)
    {
        if (!isAdmin && video.OwnerId != userId) throw new NotVideoOwnerException();
    }

    private async Task SetTagsAsync(Video video, IReadOnlyList<string> tagNames, CancellationToken ct)
    {
        var names = tagNames.Select(t => t.Trim()).Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        video.Tags.Clear();
        foreach (var name in names)
        {
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == name, ct)
                      ?? _db.Tags.Add(new Tag { Id = Guid.NewGuid(), Name = name }).Entity;
            video.Tags.Add(new VideoTag { VideoId = video.Id, TagId = tag.Id, Tag = tag });
        }
    }

    internal static async Task<PagedResult<VideoListItem>> PageAsync(IQueryable<Video> query, int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(v => new VideoListItem(v.Id, v.Title, v.Slug, v.Category!.Name, v.DurationSeconds, v.PublishedAt, v.Status.ToString()))
            .ToListAsync(ct);
        return new PagedResult<VideoListItem>(items, total, page, pageSize);
    }
}
