namespace MediaPlatform.Domain.Enums;

/// <summary>Cycle de vie d'une vidéo dans la plateforme.</summary>
public enum VideoStatus
{
    Draft = 0,
    Processing = 1,
    Ready = 2,
    Published = 3,
    Archived = 4,
    Failed = 5
}
