using MediaPlatform.Domain.Enums;

namespace MediaPlatform.Domain.Entities;

public class Comment
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public Guid UserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsModerated { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Like d'un visiteur authentifié. Unique par (VideoId, UserId).</summary>
public class Like
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Share
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public Guid? UserId { get; set; }
    public string Channel { get; set; } = string.Empty; // lien | email | réseau interne
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Événement de visionnage pour l'analytique (vues, temps de visionnage).</summary>
public class ViewEvent
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public Guid? UserId { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public double WatchSeconds { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Metadata { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Suivi d'un job de transcodage FFmpeg → HLS.</summary>
public class TranscodeJob
{
    public Guid Id { get; set; }
    public Guid VideoId { get; set; }
    public TranscodeJobStatus Status { get; set; } = TranscodeJobStatus.Queued;
    public int Progress { get; set; }
    public int Attempts { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
