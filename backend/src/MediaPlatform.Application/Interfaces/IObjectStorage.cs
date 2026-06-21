namespace MediaPlatform.Application.Interfaces;

/// <summary>Abstraction du stockage objet (implémentée par MinIO).</summary>
public interface IObjectStorage
{
    Task PutAsync(string bucket, string key, Stream content, string contentType, CancellationToken ct = default);
    Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>URL présignée pour la diffusion HLS (consommée par le lecteur).</summary>
    Task<string> GetPresignedUrlAsync(string bucket, string key, TimeSpan expiry, CancellationToken ct = default);
}

/// <summary>Abstraction du pipeline de transcodage (implémentée par FFmpeg → HLS).</summary>
public interface IVideoTranscoder
{
    /// <summary>Transcode la source en HLS multi-débit et retourne les variantes générées.</summary>
    Task<IReadOnlyList<RenditionResult>> TranscodeToHlsAsync(string sourceKey, Guid videoId, CancellationToken ct = default);
}

public record RenditionResult(string Resolution, int Bitrate, string ManifestKey);
