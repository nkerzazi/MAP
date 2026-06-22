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

/// <summary>Transcodage FFmpeg → HLS, travaillant sur le système de fichiers local.</summary>
public interface IVideoTranscoder
{
    /// <summary>Sonde la hauteur (px) et la durée (s) du fichier source.</summary>
    Task<(int Height, double DurationSeconds)> ProbeAsync(string sourcePath, CancellationToken ct = default);

    /// <summary>Transcode <paramref name="sourcePath"/> vers un échelon HLS dans <paramref name="outDir"/>
    /// (génère index.m3u8 + segments). Retourne le nom du fichier playlist relatif.</summary>
    Task<string> TranscodeRungAsync(string sourcePath, string outDir, int height, int videoKbps, int audioKbps, int segmentSeconds, CancellationToken ct = default);
}
