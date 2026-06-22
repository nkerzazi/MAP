using MediaPlatform.Domain.Enums;

namespace MediaPlatform.Domain.Entities;

/// <summary>Contenu vidéo et ses métadonnées. Agrégat racine du catalogue.</summary>
public class Video
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Slug { get; set; } = string.Empty;
    public VideoStatus Status { get; set; } = VideoStatus.Draft;

    public Guid? CategoryId { get; set; }
    public Category? Category { get; set; }

    public Guid OwnerId { get; set; }
    public User? Owner { get; set; }

    /// <summary>Clé MinIO du fichier source original.</summary>
    public string? OriginalKey { get; set; }
    public double? DurationSeconds { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Vecteur de recherche full-text (titre + description), généré par PostgreSQL.</summary>
    public NpgsqlTypes.NpgsqlTsVector SearchVector { get; set; } = null!;

    public ICollection<VideoRendition> Renditions { get; set; } = new List<VideoRendition>();
    public ICollection<VideoTag> Tags { get; set; } = new List<VideoTag>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}
