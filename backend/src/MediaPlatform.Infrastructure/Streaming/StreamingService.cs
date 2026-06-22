using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Application.Streaming;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaPlatform.Infrastructure.Streaming;

public class StreamingService : IStreamingService
{
    private readonly AppDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly string _hlsBucket;

    public StreamingService(AppDbContext db, IObjectStorage storage, IOptions<MinioOptions> minio)
    {
        _db = db; _storage = storage; _hlsBucket = minio.Value.HlsBucket;
    }

    public async Task<IReadOnlyList<RenditionInfo>> GetRenditionsAsync(Guid id, Guid? userId, bool isAdmin, CancellationToken ct = default)
    {
        var video = await _db.Videos.Include(v => v.Renditions).FirstOrDefaultAsync(v => v.Id == id, ct)
                    ?? throw new VideoNotFoundException();
        EnsureVisible(video, userId, isAdmin);
        return video.Renditions.Select(r => new RenditionInfo(r.Resolution, r.Bitrate, r.ManifestKey)).ToList();
    }

    public async Task<HlsObject> OpenHlsAsync(Guid id, string path, Guid? userId, bool isAdmin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("..") || path.StartsWith('/'))
            throw new ArgumentException("Chemin HLS invalide.", nameof(path));

        var video = await _db.Videos.FirstOrDefaultAsync(v => v.Id == id, ct) ?? throw new VideoNotFoundException();
        EnsureVisible(video, userId, isAdmin);

        var stream = await _storage.GetAsync(_hlsBucket, $"hls/{id}/{path}", ct);
        return new HlsObject(stream, ContentTypeFor(path));
    }

    private static void EnsureVisible(Video video, Guid? userId, bool isAdmin)
    {
        var visible = video.Status == VideoStatus.Published || (userId is { } u && (isAdmin || video.OwnerId == u));
        if (!visible) throw new VideoNotFoundException();
    }

    private static string ContentTypeFor(string path) =>
        path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ? "application/vnd.apple.mpegurl"
        : path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ? "video/mp2t"
        : "application/octet-stream";
}
