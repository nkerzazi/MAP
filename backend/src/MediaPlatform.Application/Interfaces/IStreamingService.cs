using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Streaming;

namespace MediaPlatform.Application.Interfaces;

public interface IStreamingService
{
    /// <summary>Renditions d'une vidéo visible (Published, ou propriétaire/Admin) ; sinon VideoNotFoundException.</summary>
    Task<IReadOnlyList<RenditionInfo>> GetRenditionsAsync(Guid id, Guid? userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Ouvre un objet HLS (manifeste/segment) après contrôle de visibilité et validation du chemin.</summary>
    Task<HlsObject> OpenHlsAsync(Guid id, string path, Guid? userId, bool isAdmin, CancellationToken ct = default);
}
