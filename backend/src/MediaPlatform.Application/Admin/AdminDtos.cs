namespace MediaPlatform.Application.Admin;

public record RecordViewRequest(double WatchSeconds, string? SessionId);
public record TopVideo(Guid Id, string Title, int Views);
public record StatsSummary(int TotalVideos, IReadOnlyDictionary<string, int> VideosByStatus,
    int TotalUsers, long TotalViews, double TotalWatchSeconds, IReadOnlyList<TopVideo> TopVideos);
public record AuditEntry(Guid Id, Guid? ActorId, string Action, string EntityType, string? EntityId, DateTimeOffset OccurredAt);
public record UpdateUserRequest(bool IsActive, string? DisplayName);
public record UserAdminItem(Guid Id, string Email, string DisplayName, bool IsActive, IReadOnlyList<string> Roles);
public record SetConfigRequest(string Key, string Value);
