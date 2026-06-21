namespace MediaPlatform.Application.Interfaces;

/// <summary>Upload chunké d'une source vidéo vers le stockage objet.</summary>
public interface IUploadService
{
    /// <summary>Stocke un chunk (idempotent). Retourne les index déjà reçus pour la vidéo.</summary>
    Task<IReadOnlyList<int>> StoreChunkAsync(Guid videoId, int index, Stream content, long size, CancellationToken ct = default);

    /// <summary>Vérifie que les <paramref name="total"/> chunks sont présents (0..total-1).
    /// Retourne le préfixe des parts à utiliser comme OriginalKey.</summary>
    Task<string> CompleteAsync(Guid videoId, int total, CancellationToken ct = default);

    /// <summary>Convention de nommage des clés (exposée pour le worker).</summary>
    static string PartsPrefix(Guid videoId) => $"originals/{videoId}/parts/";
    static string PartKey(Guid videoId, int index) => $"originals/{videoId}/parts/{index:000000}";
}
