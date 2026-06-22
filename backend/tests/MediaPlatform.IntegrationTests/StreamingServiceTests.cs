using System.Text;
using FluentAssertions;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Storage;
using MediaPlatform.Infrastructure.Streaming;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class StreamingServiceTests : IAsyncLifetime, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public StreamingServiceTests(MinioFixture minio) => _minio = minio;
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);
    private MinioObjectStorage Storage() => new(Options.Create(new MinioOptions
    { Endpoint = _minio.Endpoint, AccessKey = _minio.AccessKey, SecretKey = _minio.SecretKey, UseSsl = false }));
    private StreamingService Svc(AppDbContext db) => new(db, Storage(), Options.Create(new MinioOptions()));

    private async Task<(Guid id, Guid owner)> SeedVideoWithHls(VideoStatus status)
    {
        var owner = Guid.NewGuid(); var id = Guid.NewGuid();
        await using (var db = NewDb())
        {
            db.Users.Add(new User { Id = owner, Email = $"o{owner:N}@map.ma", DisplayName = "O", PasswordHash = "x" });
            db.Videos.Add(new Video { Id = id, Title = "T", Slug = $"s-{id:N}", OwnerId = owner, Status = status,
                Renditions = { new VideoRendition { Id = Guid.NewGuid(), Resolution = "360p", Bitrate = 800, ManifestKey = $"hls/{id}/360p/index.m3u8" } } });
            await db.SaveChangesAsync();
        }
        var storage = Storage();
        await storage.EnsureBucketAsync("hls");
        var bytes = Encoding.UTF8.GetBytes("#EXTM3U\n#EXT-X-VERSION:3\n");
        using var ms = new MemoryStream(bytes);
        await storage.PutAsync("hls", $"hls/{id}/master.m3u8", ms, bytes.Length, "application/vnd.apple.mpegurl");
        return (id, owner);
    }

    [Fact]
    public async Task OpenHls_streams_master_with_correct_content_type_for_published()
    {
        var (id, _) = await SeedVideoWithHls(VideoStatus.Published);
        await using var db = NewDb();
        var obj = await Svc(db).OpenHlsAsync(id, "master.m3u8", null, false);
        obj.ContentType.Should().Be("application/vnd.apple.mpegurl");
        (await new StreamReader(obj.Content).ReadToEndAsync()).Should().StartWith("#EXTM3U");
    }

    [Fact]
    public async Task NonPublished_is_hidden_from_anonymous_but_visible_to_owner()
    {
        var (id, owner) = await SeedVideoWithHls(VideoStatus.Ready);
        await using var db = NewDb();
        var anon = async () => await Svc(db).GetRenditionsAsync(id, null, false);
        await anon.Should().ThrowAsync<VideoNotFoundException>();
        (await Svc(db).GetRenditionsAsync(id, owner, false)).Should().ContainSingle(r => r.Resolution == "360p");
    }

    [Fact]
    public async Task OpenHls_rejects_path_traversal()
    {
        var (id, _) = await SeedVideoWithHls(VideoStatus.Published);
        await using var db = NewDb();
        var act = async () => await Svc(db).OpenHlsAsync(id, "../secret", null, false);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
