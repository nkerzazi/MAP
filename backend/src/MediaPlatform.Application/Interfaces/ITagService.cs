using MediaPlatform.Application.Catalog;

namespace MediaPlatform.Application.Interfaces;

public interface ITagService
{
    /// <summary>Liste les tags ; filtre optionnel ?q= (sous-chaîne, insensible à la casse).</summary>
    Task<IReadOnlyList<TagItem>> ListAsync(string? q, CancellationToken ct = default);
}
