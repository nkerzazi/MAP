using MediaPlatform.Application.Admin;

namespace MediaPlatform.Application.Interfaces;

public interface IAnalyticsService
{
    Task RecordViewAsync(Guid videoId, double watchSeconds, string? sessionId, Guid? userId, CancellationToken ct = default);
    Task<StatsSummary> GetStatsAsync(CancellationToken ct = default);
}
