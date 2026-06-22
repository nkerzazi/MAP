namespace MediaPlatform.Application.Catalog;

public record VideoListItem(Guid Id, string Title, string Slug, string? CategoryName,
    double? DurationSeconds, DateTimeOffset? PublishedAt, string Status);

public record VideoDetail(Guid Id, string Title, string? Description, string Slug, string Status,
    string? CategoryName, double? DurationSeconds, DateTimeOffset? PublishedAt,
    IReadOnlyList<string> Tags, IReadOnlyList<RenditionInfo> Renditions);

public record RenditionInfo(string Resolution, int Bitrate, string ManifestKey);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Critères du catalogue public.</summary>
public record CatalogQuery(string? Q, Guid? CategoryId, string? Tag, int Page = 1, int PageSize = 20);
