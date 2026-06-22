using MediaPlatform.Application.Catalog;

namespace MediaPlatform.Application.Interfaces;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryItem>> ListAsync(CancellationToken ct = default);
    Task<CategoryItem> CreateAsync(string name, Guid actorId, CancellationToken ct = default);
    Task<CategoryItem> UpdateAsync(Guid id, string name, Guid actorId, CancellationToken ct = default);
    Task DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default);
}
