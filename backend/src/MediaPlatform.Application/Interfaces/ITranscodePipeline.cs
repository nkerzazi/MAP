namespace MediaPlatform.Application.Interfaces;

/// <summary>Orchestration complète du transcodage d'une vidéo (appelée par le job Hangfire).</summary>
public interface ITranscodePipeline
{
    Task RunAsync(Guid videoId, CancellationToken ct = default);
}
