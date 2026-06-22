using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Auth;
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

public class AdminEndpointsTests : IAsyncLifetime, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private WebApplicationFactory<Program> _factory = default!;
    public AdminEndpointsTests(MinioFixture minio) => _minio = minio;

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
            b.UseSetting("Jwt:Key", "cle-de-test-tres-longue-pour-hmac-sha256-ffffffffffffffffffff");
            b.UseSetting("Jwt:Issuer", "map-media-platform"); b.UseSetting("Jwt:Audience", "map-media-platform");
            b.UseSetting("Seed:AdminEmail", "admin@map.ma"); b.UseSetting("Seed:AdminPassword", "Adm1n!seed");
        });
        _ = _factory.CreateClient();
    }
    public Task DisposeAsync() { _factory.Dispose(); return _pg.DisposeAsync().AsTask(); }
    private HttpClient Client() => _factory.CreateClient();

    private async Task<string> Login(string email, string pwd)
    {
        var resp = await (await Client().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, pwd))).Content.ReadFromJsonAsync<AuthResponse>();
        return resp!.Token;
    }
    private async Task<(string token, Guid id)> RegisterVisitor(string email)
    {
        (await Client().PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "Pass1!", "V"))).EnsureSuccessStatusCode();
        var resp = await (await Client().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "Pass1!"))).Content.ReadFromJsonAsync<AuthResponse>();
        return (resp!.Token, resp.UserId);
    }
    private static HttpClient Auth(HttpClient c, string t) { c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t); return c; }

    [Fact]
    public async Task Stats_requires_admin()
    {
        (await Client().GetAsync("/api/v1/stats")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (vtoken, _) = await RegisterVisitor("v@map.ma");
        (await Auth(Client(), vtoken).GetAsync("/api/v1/stats")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var admin = await Login("admin@map.ma", "Adm1n!seed");
        (await Auth(Client(), admin).GetAsync("/api/v1/stats")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task View_is_anonymous_and_counts_in_stats()
    {
        Guid vid;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = Guid.NewGuid(); vid = Guid.NewGuid();
            db.Users.Add(new User { Id = owner, Email = $"o{owner:N}@map.ma", DisplayName = "O", PasswordHash = "x" });
            db.Videos.Add(new Video { Id = vid, Title = "T", Slug = $"s-{vid:N}", OwnerId = owner, Status = VideoStatus.Published });
            await db.SaveChangesAsync();
        }
        (await Client().PostAsJsonAsync($"/api/v1/videos/{vid}/views", new RecordViewRequest(42.0, "s1")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var admin = await Login("admin@map.ma", "Adm1n!seed");
        var stats = await (await Auth(Client(), admin).GetAsync("/api/v1/stats")).Content.ReadFromJsonAsync<StatsSummary>();
        stats!.TotalViews.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Admin_deactivates_user_and_audit_records_it()
    {
        var (_, uid) = await RegisterVisitor("target@map.ma");
        var admin = await Login("admin@map.ma", "Adm1n!seed");

        (await Auth(Client(), admin).PutAsJsonAsync($"/api/v1/users/{uid}", new UpdateUserRequest(false, null)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var audit = await (await Auth(Client(), admin).GetAsync("/api/v1/audit")).Content.ReadAsStringAsync();
        audit.Should().Contain("user.update");
    }

    [Fact]
    public async Task Config_get_and_put_as_admin()
    {
        var admin = await Login("admin@map.ma", "Adm1n!seed");
        (await Auth(Client(), admin).PutAsJsonAsync("/api/v1/config", new SetConfigRequest("theme", "sombre")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cfg = await (await Auth(Client(), admin).GetAsync("/api/v1/config")).Content.ReadAsStringAsync();
        cfg.Should().Contain("theme").And.Contain("sombre");
    }
}
