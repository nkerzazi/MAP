using Hangfire;
using MediaPlatform.Application.Interfaces;

namespace MediaPlatform.Infrastructure.Media;

/// <summary>Point d'entrée Hangfire : délègue au pipeline. Retries gérés par l'attribut.</summary>
public class TranscodeVideoJob
{
    private readonly ITranscodePipeline _pipeline;
    public TranscodeVideoJob(ITranscodePipeline pipeline) => _pipeline = pipeline;

    [AutomaticRetry(Attempts = 3)]
    public Task ExecuteAsync(Guid videoId) => _pipeline.RunAsync(videoId, CancellationToken.None);
}
