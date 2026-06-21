namespace MediaPlatform.Domain.Enums;

/// <summary>État d'un job de transcodage FFmpeg → HLS.</summary>
public enum TranscodeJobStatus
{
    Queued = 0,
    Running = 1,
    Succeeded = 2,
    Failed = 3
}
