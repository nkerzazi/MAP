namespace MediaPlatform.Application.Interfaces;

/// <summary>Abstraction du stockage objet (implémentée par MinIO).</summary>
public interface IObjectStorage
{
    Task EnsureBucketAsync(string bucket, CancellationToken ct = default);
    Task PutAsync(string bucket, string key, Stream content, long size, string contentType, CancellationToken ct = default);
    Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>Liste les clés sous un préfixe (récursif), triées par ordre lexicographique.</summary>
    Task<IReadOnlyList<string>> ListKeysAsync(string bucket, string prefix, CancellationToken ct = default);

    /// <summary>Télécharge un objet vers un fichier local.</summary>
    Task GetToFileAsync(string bucket, string key, string destPath, CancellationToken ct = default);

    /// <summary>Supprime tous les objets sous un préfixe.</summary>
    Task DeletePrefixAsync(string bucket, string prefix, CancellationToken ct = default);

    /// <summary>URL présignée pour la diffusion HLS (consommée par le lecteur en Phase 4).</summary>
    Task<string> GetPresignedUrlAsync(string bucket, string key, TimeSpan expiry, CancellationToken ct = default);
}

/// <summary>Abstraction du pipeline de transcodage (implémentée par FFmpeg → HLS).</summary>
public interface IVideoTranscoder
{
    /// <summary>Transcode la source en HLS multi-débit et retourne les variantes générées.</summary>
    Task<IReadOnlyList<RenditionResult>> TranscodeToHlsAsync(string sourceKey, Guid videoId, CancellationToken ct = default);
}

public record RenditionResult(string Resolution, int Bitrate, string ManifestKey);
