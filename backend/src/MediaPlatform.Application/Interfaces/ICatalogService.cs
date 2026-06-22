using MediaPlatform.Application.Catalog;

namespace MediaPlatform.Application.Interfaces;

public interface ICatalogService
{
    Task<VideoDetail> UpdateAsync(Guid id, UpdateVideoRequest request, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task PublishAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task ArchiveAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<PagedResult<VideoListItem>> GetMineAsync(Guid userId, int page, CancellationToken ct = default);
    Task<PagedResult<VideoListItem>> SearchPublishedAsync(CatalogQuery query, CancellationToken ct = default);
    Task<VideoDetail> GetDetailAsync(Guid id, Guid? userId, bool isAdmin, CancellationToken ct = default);
}
