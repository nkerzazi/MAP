using FluentAssertions;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Application.Media;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Media;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.Infrastructure.Storage;
using MediaPlatform.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class TranscodePipelineTests : IAsyncLifetime, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();

    public TranscodePipelineTests(MinioFixture minio) => _minio = minio;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        await using var db = NewDb();
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();

    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_pg.GetConnectionString()).Options);

    private MinioObjectStorage Storage() => new(Options.Create(new MinioOptions
    { Endpoint = _minio.Endpoint, AccessKey = _minio.AccessKey, SecretKey = _minio.SecretKey, UseSsl = false }));

    /// <summary>Transcodeur factice : source "haute" (height 4000), produit une playlist vide.</summary>
    private sealed class FakeTranscoder : IVideoTranscoder
    {
        public Task<(int Height, double DurationSeconds)> ProbeAsync(string sourcePath, CancellationToken ct = default)
            => Task.FromResult((4000, 12.5));
        public Task<string> TranscodeRungAsync(string sourcePath, string outDir, int height, int v, int a, int seg, CancellationToken ct = default)
        {
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "index.m3u8"), "#EXTM3U");
            File.WriteAllText(Path.Combine(outDir, "seg_000.ts"), "x");
            return Task.FromResult("index.m3u8");
        }
    }

    private async Task<Guid> SeedVideoWithParts()
    {
        var owner = Guid.NewGuid(); var vid = Guid.NewGuid();
        await using (var db = NewDb())
        {
            db.Users.Add(new User { Id = owner, Email = $"e{owner:N}@map.ma", DisplayName = "E", PasswordHash = "x" });
            db.Videos.Add(new Video { Id = vid, Title = "T", Slug = $"t-{vid:N}", OwnerId = owner, Status = VideoStatus.Processing });
            await db.SaveChangesAsync();
        }
        var storage = Storage();
        await storage.EnsureBucketAsync("originals");
        var b = Encoding.UTF8.GetBytes("sourcebytes");
        using var ms = new MemoryStream(b);
        await storage.PutAsync("originals", IUploadService.PartKey(vid, 0), ms, b.Length, "application/octet-stream");
        return vid;
    }

    private TranscodePipeline NewPipeline(IVideoTranscoder transcoder) =>
        new(NewDb(), Storage(), transcoder, Options.Create(new MinioOptions()), Options.Create(new TranscodingOptions()));

    [Fact]
    public async Task Run_transcodes_to_ready_and_creates_renditions()
    {
        var vid = await SeedVideoWithParts();
        await NewPipeline(new FakeTranscoder()).RunAsync(vid);

        await using var db = NewDb();
        var video = await db.Videos.Include(v => v.Renditions).SingleAsync(v => v.Id == vid);
        video.Status.Should().Be(VideoStatus.Ready);
        video.DurationSeconds.Should().Be(12.5);
        video.Renditions.Select(r => r.Resolution).Should().BeEquivalentTo(new[] { "360p", "720p", "1080p" });
        (await Storage().ListKeysAsync("hls", $"hls/{vid}/")).Should().Contain($"hls/{vid}/master.m3u8");
        (await Storage().ListKeysAsync("originals", IUploadService.PartsPrefix(vid))).Should().BeEmpty(); // parts nettoyées
    }

    private sealed class ThrowingTranscoder : IVideoTranscoder
    {
        public Task<(int Height, double DurationSeconds)> ProbeAsync(string sourcePath, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
        public Task<string> TranscodeRungAsync(string s, string o, int h, int v, int a, int seg, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task Run_marks_failed_when_transcoder_throws()
    {
        var vid = await SeedVideoWithParts();
        var act = async () => await NewPipeline(new ThrowingTranscoder()).RunAsync(vid);
        await act.Should().ThrowAsync<Exception>();

        await using var db = NewDb();
        (await db.Videos.SingleAsync(v => v.Id == vid)).Status.Should().Be(VideoStatus.Failed);
        (await db.TranscodeJobs.SingleAsync(j => j.VideoId == vid)).Status.Should().Be(TranscodeJobStatus.Failed);
    }
}
