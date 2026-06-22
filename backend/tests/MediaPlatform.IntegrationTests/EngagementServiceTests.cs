using FluentAssertions;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Engagement;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Engagement;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class EngagementServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);
    private EngagementService Svc(AppDbContext db) => new(db);

    private async Task<Guid> NewUser(string name)
    {
        var id = Guid.NewGuid();
        await using var db = NewDb();
        db.Users.Add(new User { Id = id, Email = $"u{id:N}@map.ma", DisplayName = name, PasswordHash = "x" });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> NewVideo(Guid owner, VideoStatus status = VideoStatus.Published)
    {
        var id = Guid.NewGuid();
        await using var db = NewDb();
        db.Videos.Add(new Video { Id = id, Title = "T", Slug = $"s-{id:N}", OwnerId = owner, Status = status });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Comment_add_list_and_delete_by_author()
    {
        var u = await NewUser("Alice");
        var vid = await NewVideo(u);
        await using (var db = NewDb()) await Svc(db).AddCommentAsync(vid, "  Super vidéo  ", u);
        await using (var db = NewDb())
        {
            var page = await Svc(db).ListCommentsAsync(vid, 1, null, false);
            page.Items.Should().ContainSingle();
            page.Items[0].Body.Should().Be("Super vidéo"); // trim
            page.Items[0].AuthorDisplayName.Should().Be("Alice");
        }
        Guid commentId;
        await using (var db = NewDb())
            commentId = (await db.Comments.SingleAsync(c => c.VideoId == vid)).Id;

        await using (var db = NewDb()) await Svc(db).DeleteCommentAsync(vid, commentId, u, false);
        await using (var db = NewDb()) (await db.Comments.CountAsync(c => c.VideoId == vid)).Should().Be(0);
    }

    [Fact]
    public async Task Delete_comment_by_non_author_forbidden_admin_ok()
    {
        var author = await NewUser("A"); var other = await NewUser("B");
        var vid = await NewVideo(author);
        await using (var db = NewDb()) await Svc(db).AddCommentAsync(vid, "x", author);
        Guid cid; await using (var db = NewDb()) cid = (await db.Comments.SingleAsync()).Id;

        await using (var db = NewDb())
        {
            var act = async () => await Svc(db).DeleteCommentAsync(vid, cid, other, false);
            await act.Should().ThrowAsync<NotCommentAuthorException>();
        }
        await using (var db = NewDb()) await Svc(db).DeleteCommentAsync(vid, cid, other, true); // admin
        await using (var db = NewDb()) (await db.Comments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Like_is_idempotent_then_unlike()
    {
        var u = await NewUser("A");
        var vid = await NewVideo(u);
        await using (var db = NewDb()) await Svc(db).LikeAsync(vid, u);
        EngagementSummary s;
        await using (var db = NewDb()) s = await Svc(db).LikeAsync(vid, u); // re-like
        s.LikeCount.Should().Be(1); s.LikedByMe.Should().BeTrue();

        await using (var db = NewDb()) s = await Svc(db).UnlikeAsync(vid, u);
        s.LikeCount.Should().Be(0); s.LikedByMe.Should().BeFalse();
    }

    [Fact]
    public async Task Share_and_summary_aggregate()
    {
        var u = await NewUser("A");
        var vid = await NewVideo(u);
        await using (var db = NewDb()) await Svc(db).AddCommentAsync(vid, "c", u);
        await using (var db = NewDb()) await Svc(db).LikeAsync(vid, u);
        int shares; await using (var db = NewDb()) shares = await Svc(db).ShareAsync(vid, "lien", u);
        shares.Should().Be(1);

        await using var read = NewDb();
        var sum = await Svc(read).GetSummaryAsync(vid, u, false);
        sum.CommentCount.Should().Be(1); sum.LikeCount.Should().Be(1);
        sum.ShareCount.Should().Be(1); sum.LikedByMe.Should().BeTrue();
    }

    [Fact]
    public async Task Engaging_non_published_video_throws_not_found_for_other_users()
    {
        var owner = await NewUser("O"); var visitor = await NewUser("V");
        var vid = await NewVideo(owner, VideoStatus.Draft);
        await using var db = NewDb();
        var act = async () => await Svc(db).AddCommentAsync(vid, "x", visitor);
        await act.Should().ThrowAsync<VideoNotFoundException>();
    }
}
