using FluentAssertions;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Catalog;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class CatalogServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);
    private CatalogService Svc(AppDbContext db) => new(db);

    private async Task<(Guid videoId, Guid ownerId)> SeedVideo(VideoStatus status = VideoStatus.Ready)
    {
        var ownerId = Guid.NewGuid(); var videoId = Guid.NewGuid();
        await using var db = NewDb();
        db.Users.Add(new User { Id = ownerId, Email = $"o{ownerId:N}@map.ma", DisplayName = "O", PasswordHash = "x" });
        db.Videos.Add(new Video { Id = videoId, Title = "Titre", Slug = $"s-{videoId:N}", OwnerId = ownerId, Status = status });
        await db.SaveChangesAsync();
        return (videoId, ownerId);
    }

    [Fact]
    public async Task Update_by_owner_changes_metadata_and_tags()
    {
        var (vid, owner) = await SeedVideo();
        await using (var db = NewDb())
            await Svc(db).UpdateAsync(vid, new UpdateVideoRequest("Nouveau", "desc", null, new[] { "Politique", "politique", " Sport " }), owner, false);

        await using var check = NewDb();
        var v = await check.Videos.Include(x => x.Tags).ThenInclude(t => t.Tag).SingleAsync(x => x.Id == vid);
        v.Title.Should().Be("Nouveau");
        v.Tags.Select(t => t.Tag!.Name).Should().BeEquivalentTo(new[] { "Politique", "Sport" }); // dedup + trim
    }

    [Fact]
    public async Task Update_by_non_owner_throws_forbidden_but_admin_succeeds()
    {
        var (vid, _) = await SeedVideo();
        await using (var db = NewDb())
        {
            var act = async () => await Svc(db).UpdateAsync(vid, new UpdateVideoRequest("X", null, null, Array.Empty<string>()), Guid.NewGuid(), false);
            await act.Should().ThrowAsync<NotVideoOwnerException>();
        }
        await using (var db = NewDb())
            await Svc(db).UpdateAsync(vid, new UpdateVideoRequest("AdminEdit", null, null, Array.Empty<string>()), Guid.NewGuid(), true);
        await using var check = NewDb();
        (await check.Videos.SingleAsync(x => x.Id == vid)).Title.Should().Be("AdminEdit");
    }

    [Fact]
    public async Task Publish_requires_ready_and_archive_then_republish()
    {
        var (draft, draftOwner) = await SeedVideo(VideoStatus.Draft);
        await using (var db = NewDb())
        {
            var act = async () => await Svc(db).PublishAsync(draft, draftOwner, false);
            await act.Should().ThrowAsync<InvalidVideoStateException>();
        }

        var (vid, owner) = await SeedVideo(VideoStatus.Ready);
        await using (var db = NewDb()) await Svc(db).PublishAsync(vid, owner, false);
        await using (var db = NewDb())
        {
            var v = await db.Videos.SingleAsync(x => x.Id == vid);
            v.Status.Should().Be(VideoStatus.Published); v.PublishedAt.Should().NotBeNull();
        }
        await using (var db = NewDb()) await Svc(db).ArchiveAsync(vid, owner, false);
        await using (var db = NewDb()) (await db.Videos.SingleAsync(x => x.Id == vid)).Status.Should().Be(VideoStatus.Archived);
        await using (var db = NewDb()) await Svc(db).PublishAsync(vid, owner, false); // re-publish
        await using (var db = NewDb()) (await db.Videos.SingleAsync(x => x.Id == vid)).Status.Should().Be(VideoStatus.Published);
    }

    [Fact]
    public async Task GetMine_returns_owner_videos_all_statuses()
    {
        var (vid, owner) = await SeedVideo(VideoStatus.Draft);
        await using var db = NewDb();
        var page = await Svc(db).GetMineAsync(owner, 1);
        page.Items.Should().ContainSingle(i => i.Id == vid);
    }

    [Fact]
    public async Task Search_returns_only_published_and_matches_q()
    {
        var (pub, owner) = await SeedVideo(VideoStatus.Ready);
        await using (var db = NewDb())
        {
            await Svc(db).UpdateAsync(pub, new UpdateVideoRequest("Conseil de gouvernement", "reunion hebdomadaire", null, Array.Empty<string>()), owner, false);
            await Svc(db).PublishAsync(pub, owner, false);
        }
        await SeedVideo(VideoStatus.Draft); // ne doit pas apparaître

        await using var read = NewDb();
        var all = await Svc(read).SearchPublishedAsync(new CatalogQuery(null, null, null));
        all.Items.Should().ContainSingle(i => i.Id == pub);

        var hit = await Svc(read).SearchPublishedAsync(new CatalogQuery("gouvernement", null, null));
        hit.Items.Should().ContainSingle(i => i.Id == pub);

        var miss = await Svc(read).SearchPublishedAsync(new CatalogQuery("introuvable", null, null));
        miss.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDetail_hides_non_published_from_anonymous()
    {
        var (draft, owner) = await SeedVideo(VideoStatus.Draft);
        await using var db = NewDb();
        var anon = async () => await Svc(db).GetDetailAsync(draft, null, false);
        await anon.Should().ThrowAsync<VideoNotFoundException>();
        var asOwner = await Svc(db).GetDetailAsync(draft, owner, false);
        asOwner.Id.Should().Be(draft);
    }
}
