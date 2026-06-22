using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Engagement;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Engagement;

public class EngagementService : IEngagementService
{
    private const int PageSize = 20;
    private readonly AppDbContext _db;
    public EngagementService(AppDbContext db) => _db = db;

    public async Task<CommentItem> AddCommentAsync(Guid videoId, string body, Guid userId, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(videoId, userId, false, ct);
        var text = (body ?? string.Empty).Trim();
        if (text.Length == 0) throw new ArgumentException("Le commentaire est vide.", nameof(body));

        var comment = new Comment { Id = Guid.NewGuid(), VideoId = videoId, UserId = userId, Body = text };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync(ct);

        var name = await _db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).SingleAsync(ct);
        return new CommentItem(comment.Id, comment.Body, name, comment.CreatedAt);
    }

    public async Task<PagedResult<CommentItem>> ListCommentsAsync(Guid videoId, int page, Guid? userId, bool isAdmin, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(videoId, userId, isAdmin, ct);
        page = page < 1 ? 1 : page;
        var query = from c in _db.Comments
                    join u in _db.Users on c.UserId equals u.Id
                    where c.VideoId == videoId && !c.IsModerated
                    orderby c.CreatedAt descending
                    select new CommentItem(c.Id, c.Body, u.DisplayName, c.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        return new PagedResult<CommentItem>(items, total, page, PageSize);
    }

    public async Task DeleteCommentAsync(Guid videoId, Guid commentId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.VideoId == videoId, ct)
                      ?? throw new CommentNotFoundException();
        if (!isAdmin && comment.UserId != userId) throw new NotCommentAuthorException();
        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<EngagementSummary> LikeAsync(Guid videoId, Guid userId, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(videoId, userId, false, ct);
        if (!await _db.Likes.AnyAsync(l => l.VideoId == videoId && l.UserId == userId, ct))
        {
            _db.Likes.Add(new Like { Id = Guid.NewGuid(), VideoId = videoId, UserId = userId });
            await _db.SaveChangesAsync(ct);
        }
        return await GetSummaryAsync(videoId, userId, false, ct);
    }

    public async Task<EngagementSummary> UnlikeAsync(Guid videoId, Guid userId, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(videoId, userId, false, ct);
        var like = await _db.Likes.FirstOrDefaultAsync(l => l.VideoId == videoId && l.UserId == userId, ct);
        if (like is not null) { _db.Likes.Remove(like); await _db.SaveChangesAsync(ct); }
        return await GetSummaryAsync(videoId, userId, false, ct);
    }

    public async Task<int> ShareAsync(Guid videoId, string channel, Guid userId, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(videoId, userId, false, ct);
        var ch = (channel ?? string.Empty).Trim();
        if (ch.Length == 0) throw new ArgumentException("Canal de partage vide.", nameof(channel));
        _db.Shares.Add(new Share { Id = Guid.NewGuid(), VideoId = videoId, UserId = userId, Channel = ch });
        await _db.SaveChangesAsync(ct);
        return await _db.Shares.CountAsync(s => s.VideoId == videoId, ct);
    }

    public async Task<EngagementSummary> GetSummaryAsync(Guid videoId, Guid? userId, bool isAdmin, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(videoId, userId, isAdmin, ct);
        var likeCount = await _db.Likes.CountAsync(l => l.VideoId == videoId, ct);
        var commentCount = await _db.Comments.CountAsync(c => c.VideoId == videoId && !c.IsModerated, ct);
        var shareCount = await _db.Shares.CountAsync(s => s.VideoId == videoId, ct);
        var likedByMe = userId is { } u && await _db.Likes.AnyAsync(l => l.VideoId == videoId && l.UserId == u, ct);
        return new EngagementSummary(likeCount, commentCount, shareCount, likedByMe);
    }

    private async Task EnsureVisibleAsync(Guid videoId, Guid? userId, bool isAdmin, CancellationToken ct)
    {
        var video = await _db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == videoId, ct)
                    ?? throw new VideoNotFoundException();
        var visible = video.Status == VideoStatus.Published || (userId is { } u && (isAdmin || video.OwnerId == u));
        if (!visible) throw new VideoNotFoundException();
    }
}
