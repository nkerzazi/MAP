using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Admin;

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _db;
    public AnalyticsService(AppDbContext db) => _db = db;

    public async Task RecordViewAsync(Guid videoId, double watchSeconds, string? sessionId, Guid? userId, CancellationToken ct = default)
    {
        var published = await _db.Videos.AnyAsync(v => v.Id == videoId && v.Status == VideoStatus.Published, ct);
        if (!published) throw new VideoNotFoundException();

        _db.ViewEvents.Add(new ViewEvent
        {
            Id = Guid.NewGuid(), VideoId = videoId, UserId = userId,
            SessionId = sessionId ?? string.Empty,
            WatchSeconds = watchSeconds < 0 ? 0 : watchSeconds
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<StatsSummary> GetStatsAsync(CancellationToken ct = default)
    {
        var totalVideos = await _db.Videos.CountAsync(ct);
        var byStatus = await _db.Videos.GroupBy(v => v.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var totalUsers = await _db.Users.CountAsync(ct);
        var totalViews = await _db.ViewEvents.LongCountAsync(ct);
        var totalWatch = await _db.ViewEvents.SumAsync(v => (double?)v.WatchSeconds, ct) ?? 0;

        var top = await _db.ViewEvents.GroupBy(v => v.VideoId)
            .Select(g => new { VideoId = g.Key, Views = g.Count() })
            .OrderByDescending(x => x.Views).Take(5).ToListAsync(ct);
        var topIds = top.Select(t => t.VideoId).ToList();
        var titles = await _db.Videos.Where(v => topIds.Contains(v.Id))
            .Select(v => new { v.Id, v.Title }).ToListAsync(ct);
        var topVideos = top
            .Select(t => new TopVideo(t.VideoId, titles.FirstOrDefault(x => x.Id == t.VideoId)?.Title ?? "", t.Views))
            .ToList();

        return new StatsSummary(totalVideos,
            byStatus.ToDictionary(s => s.Key.ToString(), s => s.Count),
            totalUsers, totalViews, totalWatch, topVideos);
    }
}
