using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MediaPlatform.Application.Auth;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Engagement;
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

public class EngagementEndpointsTests : IAsyncLifetime, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private WebApplicationFactory<Program> _factory = default!;
    public EngagementEndpointsTests(MinioFixture minio) => _minio = minio;

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
            b.UseSetting("Jwt:Key", "cle-de-test-tres-longue-pour-hmac-sha256-eeeeeeeeeeeeeeeeeeee");
            b.UseSetting("Jwt:Issuer", "map-media-platform"); b.UseSetting("Jwt:Audience", "map-media-platform");
            b.UseSetting("Seed:AdminEmail", ""); b.UseSetting("Seed:AdminPassword", "");
        });
        _ = _factory.CreateClient();
    }
    public Task DisposeAsync() { _factory.Dispose(); return _pg.DisposeAsync().AsTask(); }
    private HttpClient Client() => _factory.CreateClient();

    private async Task<(string token, Guid userId)> CreateVisitor(string email)
    {
        var c = Client();
        (await c.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "Pass1!", "Visiteur"))).EnsureSuccessStatusCode();
        var resp = await (await c.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Pass1!"))).Content.ReadFromJsonAsync<AuthResponse>();
        return (resp!.Token, resp.UserId);
    }

    private async Task<Guid> SeedPublished(Guid owner)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = Guid.NewGuid();
        db.Videos.Add(new Video { Id = id, Title = "T", Slug = $"s-{id:N}", OwnerId = owner, Status = VideoStatus.Published });
        await db.SaveChangesAsync();
        return id;
    }

    private static HttpClient Auth(HttpClient c, string token)
    { c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); return c; }

    [Fact]
    public async Task Comment_requires_auth_then_visible_in_list()
    {
        var (token, uid) = await CreateVisitor("v1@map.ma");
        var vid = await SeedPublished(uid);

        (await Client().PostAsJsonAsync($"/api/v1/videos/{vid}/comments", new CreateCommentRequest("Bravo")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await Auth(Client(), token).PostAsJsonAsync($"/api/v1/videos/{vid}/comments", new CreateCommentRequest("Bravo")))
            .EnsureSuccessStatusCode();

        var page = await (await Client().GetAsync($"/api/v1/videos/{vid}/comments")).Content.ReadFromJsonAsync<PagedResult<CommentItem>>();
        page!.Items.Should().ContainSingle(i => i.Body == "Bravo" && i.AuthorDisplayName == "Visiteur");
    }

    [Fact]
    public async Task Like_unlike_flow_and_summary()
    {
        var (token, uid) = await CreateVisitor("v2@map.ma");
        var vid = await SeedPublished(uid);

        var liked = await (await Auth(Client(), token).PostAsync($"/api/v1/videos/{vid}/likes", null))
            .Content.ReadFromJsonAsync<EngagementSummary>();
        liked!.LikeCount.Should().Be(1); liked.LikedByMe.Should().BeTrue();

        var summary = await (await Client().GetAsync($"/api/v1/videos/{vid}/engagement")).Content.ReadFromJsonAsync<EngagementSummary>();
        summary!.LikeCount.Should().Be(1);

        var unliked = await (await Auth(Client(), token).DeleteAsync($"/api/v1/videos/{vid}/likes"))
            .Content.ReadFromJsonAsync<EngagementSummary>();
        unliked!.LikeCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_comment_by_non_author_is_forbidden()
    {
        var (tokenA, uidA) = await CreateVisitor("a2@map.ma");
        var (tokenB, _) = await CreateVisitor("b2@map.ma");
        var vid = await SeedPublished(uidA);
        var created = await (await Auth(Client(), tokenA).PostAsJsonAsync($"/api/v1/videos/{vid}/comments", new CreateCommentRequest("hi")))
            .Content.ReadFromJsonAsync<CommentItem>();

        (await Auth(Client(), tokenB).DeleteAsync($"/api/v1/videos/{vid}/comments/{created!.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
