namespace MediaPlatform.Application.Engagement;

public record CreateCommentRequest(string Body);
public record ShareRequest(string Channel);
public record CommentItem(Guid Id, string Body, string AuthorDisplayName, DateTimeOffset CreatedAt);
public record EngagementSummary(int LikeCount, int CommentCount, int ShareCount, bool LikedByMe);
