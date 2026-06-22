namespace MediaPlatform.Application.Catalog;

public record UpdateVideoRequest(string Title, string? Description, Guid? CategoryId, IReadOnlyList<string> Tags);
