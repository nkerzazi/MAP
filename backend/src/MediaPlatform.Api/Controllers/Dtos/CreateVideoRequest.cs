namespace MediaPlatform.Api.Controllers.Dtos;

public record CreateVideoRequest(string Title, string? Description, Guid? CategoryId);
