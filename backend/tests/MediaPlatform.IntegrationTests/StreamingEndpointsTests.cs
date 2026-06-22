using System.Net;
using System.Text;
using FluentAssertions;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class StreamingEndpointsTests : IAsyncLifetime, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private WebApplicationFactory<Program> _factory = default!;
    public StreamingEndpointsTests(MinioFixture minio) => _minio = minio;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:Postgres", _pg.GetConnectionString());
            b.UseSetting("Minio:Endpoint", _minio.Endpoint);
            b.UseSetting("Minio:AccessKey", _minio.AccessKey);
            b.UseSetting("Minio:SecretKey", _minio.SecretKey);
            b.UseSetting("Minio:UseSsl", "false");
            b.UseSetting("Jwt:Key", "cle-de-test-tres-longue-pour-hmac-sha256-dddddddddddddddddddd");
            b.UseSetting("Jwt:Issuer", "map-media-platform"); b.UseSetting("Jwt:Audience", "map-media-platform");
            b.UseSetting("Seed:AdminEmail", ""); b.UseSetting("Seed:AdminPassword", "");
        });
        _ = _factory.CreateClient();
    }
    public Task DisposeAsync() { _factory.Dispose(); return _pg.DisposeAsync().AsTask(); }
    private HttpClient Client() => _factory.CreateClient();

    private async Task<Guid> SeedPublishedWithHls()
    {
        Guid id = Guid.NewGuid(), owner = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Add(new User { Id = owner, Email = $"o{owner:N}@map.ma", DisplayName = "O", PasswordHash = "x" });
            db.Videos.Add(new Video { Id = id, Title = "T", Slug = $"s-{id:N}", OwnerId = owner, Status = VideoStatus.Published });
            await db.SaveChangesAsync();
            var storage = scope.ServiceProvider.GetRequiredService<IObjectStorage>();
            await storage.EnsureBucketAsync("hls");
            var bytes = Encoding.UTF8.GetBytes("#EXTM3U");
            using var ms = new MemoryStream(bytes);
            await storage.PutAsync("hls", $"hls/{id}/master.m3u8", ms, bytes.Length, "application/vnd.apple.mpegurl");
        }
        return id;
    }

    [Fact]
    public async Task Hls_serves_master_for_published()
    {
        var id = await SeedPublishedWithHls();
        var resp = await Client().GetAsync($"/api/v1/videos/{id}/hls/master.m3u8");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.apple.mpegurl");
        (await resp.Content.ReadAsStringAsync()).Should().StartWith("#EXTM3U");
    }

    [Fact]
    public async Task Stream_returns_manifest_url()
    {
        var id = await SeedPublishedWithHls();
        var resp = await Client().GetStringAsync($"/api/v1/videos/{id}/stream");
        resp.Should().Contain($"/api/v1/videos/{id}/hls/master.m3u8");
    }

    [Fact]
    public async Task Hls_404_for_unknown_video()
    {
        (await Client().GetAsync($"/api/v1/videos/{Guid.NewGuid()}/hls/master.m3u8")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }
}
