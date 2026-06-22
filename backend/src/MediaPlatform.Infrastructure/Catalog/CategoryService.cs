using System.Globalization;
using System.Text;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Catalog;

public class CategoryService : ICategoryService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    public CategoryService(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<IReadOnlyList<CategoryItem>> ListAsync(CancellationToken ct = default) =>
        await _db.Categories.OrderBy(c => c.Name)
            .Select(c => new CategoryItem(c.Id, c.Name, c.Slug))
            .ToListAsync(ct);

    public async Task<CategoryItem> CreateAsync(string name, Guid actorId, CancellationToken ct = default)
    {
        name = Clean(name);
        await EnsureUniqueAsync(name, null, ct);
        var category = new Category { Id = Guid.NewGuid(), Name = name, Slug = await UniqueSlugAsync(name, null, ct) };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "category.create", "Category", category.Id.ToString(), name, ct);
        return new CategoryItem(category.Id, category.Name, category.Slug);
    }

    public async Task<CategoryItem> UpdateAsync(Guid id, string name, Guid actorId, CancellationToken ct = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new CategoryNotFoundException(id);
        name = Clean(name);
        await EnsureUniqueAsync(name, id, ct);
        category.Name = name;
        category.Slug = await UniqueSlugAsync(name, id, ct);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "category.update", "Category", id.ToString(), name, ct);
        return new CategoryItem(category.Id, category.Name, category.Slug);
    }

    public async Task DeleteAsync(Guid id, Guid actorId, CancellationToken ct = default)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new CategoryNotFoundException(id);
        if (await _db.Videos.AnyAsync(v => v.CategoryId == id, ct))
            throw new CategoryInUseException(id);
        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "category.delete", "Category", id.ToString(), category.Name, ct);
    }

    // --- helpers ---

    private static string Clean(string name)
    {
        name = (name ?? string.Empty).Trim();
        if (name.Length == 0) throw new ArgumentException("Le nom de la catégorie est requis.");
        return name;
    }

    private async Task EnsureUniqueAsync(string name, Guid? excludeId, CancellationToken ct)
    {
        var lowered = name.ToLower();
        if (await _db.Categories.AnyAsync(c => c.Name.ToLower() == lowered && (excludeId == null || c.Id != excludeId), ct))
            throw new DuplicateCategoryException(name);
    }

    private async Task<string> UniqueSlugAsync(string name, Guid? excludeId, CancellationToken ct)
    {
        var baseSlug = Slugify(name);
        if (baseSlug.Length == 0) baseSlug = "categorie";
        var slug = baseSlug;
        var n = 2;
        while (await _db.Categories.AnyAsync(c => c.Slug == slug && (excludeId == null || c.Id != excludeId), ct))
            slug = $"{baseSlug}-{n++}";
        return slug.Length > 160 ? slug[..160] : slug;
    }

    private static string Slugify(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-');
        }
        var slug = sb.ToString();
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }
}
