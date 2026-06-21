using System.Text;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Application.Media;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaPlatform.Infrastructure.Media;

public class TranscodePipeline : ITranscodePipeline
{
    private readonly AppDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly IVideoTranscoder _transcoder;
    private readonly MinioOptions _minio;
    private readonly TranscodingOptions _opts;

    public TranscodePipeline(AppDbContext db, IObjectStorage storage, IVideoTranscoder transcoder,
        IOptions<MinioOptions> minio, IOptions<TranscodingOptions> opts)
    {
        _db = db; _storage = storage; _transcoder = transcoder;
        _minio = minio.Value; _opts = opts.Value;
    }

    public async Task RunAsync(Guid videoId, CancellationToken ct = default)
    {
        var video = await _db.Videos.SingleAsync(v => v.Id == videoId, ct);
        var job = await _db.TranscodeJobs.FirstOrDefaultAsync(j => j.VideoId == videoId, ct);
        if (job is null)
        {
            job = new TranscodeJob { Id = Guid.NewGuid(), VideoId = videoId, CreatedAt = DateTimeOffset.UtcNow };
            _db.TranscodeJobs.Add(job);
        }
        job.Status = TranscodeJobStatus.Running;
        job.Attempts += 1;
        video.Status = VideoStatus.Processing;
        await _db.SaveChangesAsync(ct);

        var work = Directory.CreateTempSubdirectory("map-transcode-");
        try
        {
            // 1. Télécharger + concaténer les parts dans l'ordre.
            var sourcePath = Path.Combine(work.FullName, "source.bin");
            var parts = await _storage.ListKeysAsync(_minio.OriginalsBucket, IUploadService.PartsPrefix(videoId), ct);
            await using (var src = File.Create(sourcePath))
            {
                foreach (var key in parts) // ListKeysAsync trie en ordinal => ordre des index
                {
                    var partPath = Path.Combine(work.FullName, "part.tmp");
                    await _storage.GetToFileAsync(_minio.OriginalsBucket, key, partPath, ct);
                    await using var pin = File.OpenRead(partPath);
                    await pin.CopyToAsync(src, ct);
                }
            }

            // 2. Sonder + sélectionner la ladder.
            var (height, duration) = await _transcoder.ProbeAsync(sourcePath, ct);
            var rungs = HlsLadder.Select(height, _opts);

            // 3. Transcoder chaque échelon + uploader.
            await _storage.EnsureBucketAsync(_minio.HlsBucket, ct);
            var master = new StringBuilder("#EXTM3U\n#EXT-X-VERSION:3\n");
            foreach (var rung in rungs)
            {
                var rungDir = Path.Combine(work.FullName, rung.Name);
                var playlist = await _transcoder.TranscodeRungAsync(sourcePath, rungDir, rung.Height,
                    rung.VideoKbps, rung.AudioKbps, _opts.SegmentSeconds, ct);

                foreach (var f in Directory.EnumerateFiles(rungDir))
                {
                    var key = $"hls/{videoId}/{rung.Name}/{Path.GetFileName(f)}";
                    await using var fin = File.OpenRead(f);
                    var type = f.EndsWith(".m3u8") ? "application/vnd.apple.mpegurl" : "video/mp2t";
                    await _storage.PutAsync(_minio.HlsBucket, key, fin, fin.Length, type, ct);
                }

                var bandwidth = (rung.VideoKbps + rung.AudioKbps) * 1000;
                master.Append($"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION=x{rung.Height}\n");
                master.Append($"{rung.Name}/{playlist}\n");

                _db.VideoRenditions.Add(new VideoRendition
                {
                    Id = Guid.NewGuid(), VideoId = videoId, Resolution = rung.Name,
                    Bitrate = rung.VideoKbps, ManifestKey = $"hls/{videoId}/{rung.Name}/{playlist}"
                });
            }

            // 4. Manifeste maître.
            var masterBytes = Encoding.UTF8.GetBytes(master.ToString());
            using (var ms = new MemoryStream(masterBytes))
                await _storage.PutAsync(_minio.HlsBucket, $"hls/{videoId}/master.m3u8", ms, masterBytes.Length,
                    "application/vnd.apple.mpegurl", ct);

            // 5. Finaliser.
            video.DurationSeconds = duration;
            video.Status = VideoStatus.Ready;
            video.UpdatedAt = DateTimeOffset.UtcNow;
            job.Status = TranscodeJobStatus.Succeeded;
            await _db.SaveChangesAsync(ct);

            await _storage.DeletePrefixAsync(_minio.OriginalsBucket, IUploadService.PartsPrefix(videoId), ct);
        }
        catch (Exception ex)
        {
            video.Status = VideoStatus.Failed;
            job.Status = TranscodeJobStatus.Failed;
            job.Error = ex.Message;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw; // laisse Hangfire gérer le retry
        }
        finally
        {
            try { work.Delete(recursive: true); } catch { /* best effort */ }
        }
    }
}
