using FluentAssertions;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Admin;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class AnalyticsServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);
    private AnalyticsService Svc(AppDbContext db) => new(db);

    private async Task<Guid> SeedPublishedVideo()
    {
        var owner = Guid.NewGuid(); var vid = Guid.NewGuid();
        await using var db = NewDb();
        db.Users.Add(new User { Id = owner, Email = $"o{owner:N}@map.ma", DisplayName = "O", PasswordHash = "x" });
        db.Videos.Add(new Video { Id = vid, Title = "Vedette", Slug = $"s-{vid:N}", OwnerId = owner, Status = VideoStatus.Published });
        await db.SaveChangesAsync();
        return vid;
    }

    [Fact]
    public async Task RecordView_anonymous_then_stats_aggregate()
    {
        var vid = await SeedPublishedVideo();
        await using (var db = NewDb()) await Svc(db).RecordViewAsync(vid, 30.0, "sess-1", null);
        await using (var db = NewDb()) await Svc(db).RecordViewAsync(vid, 20.0, "sess-2", null);

        await using var read = NewDb();
        var stats = await Svc(read).GetStatsAsync();
        stats.TotalVideos.Should().Be(1);
        stats.VideosByStatus["Published"].Should().Be(1);
        stats.TotalViews.Should().Be(2);
        stats.TotalWatchSeconds.Should().Be(50.0);
        stats.TopVideos.Should().ContainSingle(t => t.Id == vid && t.Views == 2);
    }

    [Fact]
    public async Task RecordView_on_non_published_throws()
    {
        var owner = Guid.NewGuid(); var vid = Guid.NewGuid();
        await using (var db = NewDb())
        {
            db.Users.Add(new User { Id = owner, Email = $"o{owner:N}@map.ma", DisplayName = "O", PasswordHash = "x" });
            db.Videos.Add(new Video { Id = vid, Title = "Draft", Slug = $"s-{vid:N}", OwnerId = owner, Status = VideoStatus.Draft });
            await db.SaveChangesAsync();
        }
        await using var db2 = NewDb();
        var act = async () => await Svc(db2).RecordViewAsync(vid, 10, null, null);
        await act.Should().ThrowAsync<VideoNotFoundException>();
    }
}
