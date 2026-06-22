using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using MediaPlatform.Application.Auth;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class AuthEndpointsTests : IAsyncLifetime, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private WebApplicationFactory<Program> _factory = default!;

    public AuthEndpointsTests(MinioFixture minio) => _minio = minio;

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
            b.UseSetting("Jwt:Key", "cle-de-test-tres-longue-pour-hmac-sha256-bbbbbbbbbbbbbbbbbbbb");
            b.UseSetting("Jwt:Issuer", "map-media-platform");
            b.UseSetting("Jwt:Audience", "map-media-platform");
            b.UseSetting("Seed:AdminEmail", "");   // pas de seed auto ici
            b.UseSetting("Seed:AdminPassword", "");
        });
        _ = _factory.CreateClient(); // force le démarrage (migrations + buckets)
    }

    public Task DisposeAsync() { _factory.Dispose(); return _pg.DisposeAsync().AsTask(); }

    private HttpClient Client() => _factory.CreateClient();

    private async Task Register(string email, string password, string display) =>
        (await Client().PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, password, display)))
            .EnsureSuccessStatusCode();

    private async Task<string> Login(string email, string password)
    {
        var resp = await (await Client().PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password)))
            .Content.ReadFromJsonAsync<AuthResponse>();
        return resp!.Token;
    }

    private async Task<string> RegisterAndLogin(string email, string password, string display)
    {
        await Register(email, password, display);
        return await Login(email, password);
    }

    private async Task PromoteToAdmin(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.Include(u => u.Roles).SingleAsync(u => u.Email == email.ToLowerInvariant());
        var admin = await db.Roles.SingleAsync(r => r.Name == "Admin");
        if (user.Roles.All(r => r.RoleId != admin.Id))
        { user.Roles.Add(new UserRole { UserId = user.Id, RoleId = admin.Id }); await db.SaveChangesAsync(); }
    }

    [Fact]
    public async Task Users_endpoint_enforces_admin_role()
    {
        // anonyme → 401
        (await Client().GetAsync("/api/v1/users")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // visiteur → 403
        var visiteurToken = await RegisterAndLogin("visiteur@map.ma", "Pass1!", "V");
        var c1 = Client(); c1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", visiteurToken);
        (await c1.GetAsync("/api/v1/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // admin → 200
        await Register("boss@map.ma", "Pass1!", "Boss");
        await PromoteToAdmin("boss@map.ma");
        var adminToken = await Login("boss@map.ma", "Pass1!"); // re-login → token avec rôle Admin
        var c2 = Client(); c2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        (await c2.GetAsync("/api/v1/users")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_duplicate_email_returns_409()
    {
        var c = Client();
        (await c.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("dupe@map.ma", "Pass1!", "D")))
            .EnsureSuccessStatusCode();
        (await c.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("dupe@map.ma", "Pass1!", "D")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Phase2_upload_endpoint_returns_401_without_token()
    {
        // Vérifie que l'ajout de l'auth corrige le 500 observé : VideosController.Create est [Authorize(Roles=Editeur)]
        (await Client().PostAsJsonAsync("/api/v1/videos", new { title = "x" }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
