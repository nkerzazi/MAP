namespace MediaPlatform.Application.Catalog;

/// <summary>Catégorie du catalogue (référence pour filtres et formulaires).</summary>
public record CategoryItem(Guid Id, string Name, string Slug);

public record CreateCategoryRequest(string Name);
public record UpdateCategoryRequest(string Name);

/// <summary>Tag pour l'autocomplétion côté éditeur et le filtrage du catalogue.</summary>
public record TagItem(Guid Id, string Name);
