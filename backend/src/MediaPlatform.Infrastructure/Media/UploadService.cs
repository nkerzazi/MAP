using MediaPlatform.Application.Interfaces;
using MediaPlatform.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace MediaPlatform.Infrastructure.Media;

public class UploadService : IUploadService
{
    private readonly IObjectStorage _storage;
    private readonly string _bucket;

    public UploadService(IObjectStorage storage, IOptions<MinioOptions> minio)
    {
        _storage = storage;
        _bucket = minio.Value.OriginalsBucket;
    }

    public async Task<IReadOnlyList<int>> StoreChunkAsync(Guid videoId, int index, Stream content, long size, CancellationToken ct = default)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        await _storage.EnsureBucketAsync(_bucket, ct);
        await _storage.PutAsync(_bucket, IUploadService.PartKey(videoId, index), content, size, "application/octet-stream", ct);
        return await ReceivedIndicesAsync(videoId, ct);
    }

    public async Task<string> CompleteAsync(Guid videoId, int total, CancellationToken ct = default)
    {
        if (total <= 0) throw new ArgumentOutOfRangeException(nameof(total));
        var received = (await ReceivedIndicesAsync(videoId, ct)).ToHashSet();
        for (var i = 0; i < total; i++)
            if (!received.Contains(i))
                throw new InvalidOperationException($"Chunk {i} manquant pour la vidéo {videoId}.");
        return IUploadService.PartsPrefix(videoId);
    }

    private async Task<IReadOnlyList<int>> ReceivedIndicesAsync(Guid videoId, CancellationToken ct)
    {
        var keys = await _storage.ListKeysAsync(_bucket, IUploadService.PartsPrefix(videoId), ct);
        return keys
            .Select(k => k[(k.LastIndexOf('/') + 1)..])
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .OrderBy(i => i)
            .ToList();
    }
}
