namespace MediaPlatform.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<VideoTag> Videos { get; set; } = new List<VideoTag>();
}

public class VideoTag
{
    public Guid VideoId { get; set; }
    public Video? Video { get; set; }
    public Guid TagId { get; set; }
    public Tag? Tag { get; set; }
}

/// <summary>Variante HLS d'une vidéo (ex. 720p) générée par FFmpeg.</summary>
public class VideoRendition
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public Video? Video { get; set; }
    public string Resolution { get; set; } = string.Empty; // 360p | 720p | 1080p
    public int Bitrate { get; set; }
    public string ManifestKey { get; set; } = string.Empty; // clé MinIO du .m3u8
}
