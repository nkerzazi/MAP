using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Engagement;

namespace MediaPlatform.Application.Interfaces;

public interface IEngagementService
{
    Task<CommentItem> AddCommentAsync(Guid videoId, string body, Guid userId, CancellationToken ct = default);
    Task<PagedResult<CommentItem>> ListCommentsAsync(Guid videoId, int page, Guid? userId, bool isAdmin, CancellationToken ct = default);
    Task DeleteCommentAsync(Guid videoId, Guid commentId, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<EngagementSummary> LikeAsync(Guid videoId, Guid userId, CancellationToken ct = default);
    Task<EngagementSummary> UnlikeAsync(Guid videoId, Guid userId, CancellationToken ct = default);
    Task<int> ShareAsync(Guid videoId, string channel, Guid userId, CancellationToken ct = default);
    Task<EngagementSummary> GetSummaryAsync(Guid videoId, Guid? userId, bool isAdmin, CancellationToken ct = default);
}
