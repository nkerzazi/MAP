using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MediaPlatform.Application.Auth;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class CatalogEndpointsTests : IAsyncLifetime, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private WebApplicationFactory<Program> _factory = default!;
    public CatalogEndpointsTests(MinioFixture minio) => _minio = minio;

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
            b.UseSetting("Jwt:Key", "cle-de-test-tres-longue-pour-hmac-sha256-cccccccccccccccccccc");
            b.UseSetting("Jwt:Issuer", "map-media-platform");
            b.UseSetting("Jwt:Audience", "map-media-platform");
            b.UseSetting("Seed:AdminEmail", ""); b.UseSetting("Seed:AdminPassword", "");
        });
        _ = _factory.CreateClient();
    }
    public Task DisposeAsync() { _factory.Dispose(); return _pg.DisposeAsync().AsTask(); }
    private HttpClient Client() => _factory.CreateClient();

    private async Task<(string token, Guid userId)> CreateEditor(string email)
    {
        var c = Client();
        (await c.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "Pass1!", "Ed"))).EnsureSuccessStatusCode();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.Include(u => u.Roles).SingleAsync(u => u.Email == email);
            var ed = await db.Roles.SingleAsync(r => r.Name == "Editeur");
            if (user.Roles.All(r => r.RoleId != ed.Id)) { user.Roles.Add(new UserRole { UserId = user.Id, RoleId = ed.Id }); await db.SaveChangesAsync(); }
        }
        var resp = await (await c.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Pass1!"))).Content.ReadFromJsonAsync<AuthResponse>();
        return (resp!.Token, resp.UserId);
    }

    private async Task<Guid> SeedReadyVideo(Guid ownerId, string title)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        db.Videos.Add(new Video { Id = id, Title = title, Slug = $"s-{id:N}", OwnerId = ownerId, Status = VideoStatus.Ready });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Editor_publishes_then_video_appears_in_public_catalog()
    {
        var (token, ownerId) = await CreateEditor("ed@map.ma");
        var vid = await SeedReadyVideo(ownerId, "Reportage special");
        var c = Client(); c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (await c.PostAsync($"/api/v1/videos/{vid}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await (await Client().GetAsync("/api/v1/videos?q=special")).Content.ReadFromJsonAsync<PagedResult<VideoListItem>>();
        page!.Items.Should().ContainSingle(i => i.Id == vid);
    }

    [Fact]
    public async Task Editor_actions_require_auth_and_ownership()
    {
        var (tokenA, ownerA) = await CreateEditor("a@map.ma");
        var (tokenB, _) = await CreateEditor("b@map.ma");
        var vid = await SeedReadyVideo(ownerA, "Titre A");

        // anonyme → 401
        (await Client().PostAsync($"/api/v1/videos/{vid}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // éditeur non-propriétaire → 403
        var cb = Client(); cb.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenB);
        (await cb.PostAsync($"/api/v1/videos/{vid}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // propriétaire → 200
        var ca = Client(); ca.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenA);
        (await ca.PostAsync($"/api/v1/videos/{vid}/publish", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Non_published_detail_hidden_from_anonymous()
    {
        var (_, ownerId) = await CreateEditor("c@map.ma");
        var vid = await SeedReadyVideo(ownerId, "Brouillon"); // Ready (non publié) → caché à l'anonyme
        (await Client().GetAsync($"/api/v1/videos/{vid}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
