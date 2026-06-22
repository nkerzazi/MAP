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

/// <summary>
/// Endpoints de référence du catalogue : catégories (liste publique + CRUD Admin)
/// et tags (liste/autocomplétion). Pré-requis backend du frontend.
/// </summary>
public class CategoryTagEndpointsTests : IAsyncLifetime, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private WebApplicationFactory<Program> _factory = default!;
    public CategoryTagEndpointsTests(MinioFixture minio) => _minio = minio;

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
            b.UseSetting("Jwt:Issuer", "map-media-platform");
            b.UseSetting("Jwt:Audience", "map-media-platform");
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
    private async Task<string> RegisterVisitor(string email)
    {
        (await Client().PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "Pass1!", "V"))).EnsureSuccessStatusCode();
        return await Login(email, "Pass1!");
    }
    private static HttpClient Auth(HttpClient c, string t) { c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t); return c; }

    [Fact]
    public async Task Categories_list_is_public_and_shows_created_categories()
    {
        var admin = await Login("admin@map.ma", "Adm1n!seed");
        var created = await (await Auth(Client(), admin).PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Actualités")))
            .Content.ReadFromJsonAsync<CategoryItem>();
        created!.Slug.Should().Be("actualites");

        // anonyme peut lister
        var list = await (await Client().GetAsync("/api/v1/categories")).Content.ReadFromJsonAsync<List<CategoryItem>>();
        list!.Should().ContainSingle(c => c.Id == created.Id && c.Name == "Actualités");
    }

    [Fact]
    public async Task Create_category_requires_admin_role()
    {
        (await Client().PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Sport")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var visitor = await RegisterVisitor("v@map.ma");
        (await Auth(Client(), visitor).PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Sport")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_duplicate_category_returns_conflict()
    {
        var admin = await Login("admin@map.ma", "Adm1n!seed");
        (await Auth(Client(), admin).PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Culture")))
            .EnsureSuccessStatusCode();
        // même nom (insensible à la casse) → 409
        (await Auth(Client(), admin).PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("culture")))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Admin_renames_and_deletes_unused_category()
    {
        var admin = await Login("admin@map.ma", "Adm1n!seed");
        var cat = await (await Auth(Client(), admin).PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Economie")))
            .Content.ReadFromJsonAsync<CategoryItem>();

        (await Auth(Client(), admin).PutAsJsonAsync($"/api/v1/categories/{cat!.Id}", new UpdateCategoryRequest("Économie nationale")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await Auth(Client(), admin).DeleteAsync($"/api/v1/categories/{cat.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await (await Client().GetAsync("/api/v1/categories")).Content.ReadFromJsonAsync<List<CategoryItem>>();
        list!.Should().NotContain(c => c.Id == cat.Id);
    }

    [Fact]
    public async Task Delete_category_in_use_returns_conflict()
    {
        var admin = await Login("admin@map.ma", "Adm1n!seed");
        var cat = await (await Auth(Client(), admin).PostAsJsonAsync("/api/v1/categories", new CreateCategoryRequest("Politique")))
            .Content.ReadFromJsonAsync<CategoryItem>();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = Guid.NewGuid();
            db.Users.Add(new User { Id = owner, Email = $"o{owner:N}@map.ma", DisplayName = "O", PasswordHash = "x" });
            db.Videos.Add(new Video { Id = Guid.NewGuid(), Title = "T", Slug = $"s-{Guid.NewGuid():N}", OwnerId = owner, CategoryId = cat!.Id, Status = VideoStatus.Published });
            await db.SaveChangesAsync();
        }

        (await Auth(Client(), admin).DeleteAsync($"/api/v1/categories/{cat!.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Tags_list_returns_existing_tags_with_optional_filter()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Tags.Add(new Tag { Id = Guid.NewGuid(), Name = "élection" });
            db.Tags.Add(new Tag { Id = Guid.NewGuid(), Name = "économie" });
            db.Tags.Add(new Tag { Id = Guid.NewGuid(), Name = "sport" });
            await db.SaveChangesAsync();
        }

        var all = await (await Client().GetAsync("/api/v1/tags")).Content.ReadFromJsonAsync<List<TagItem>>();
        all!.Select(t => t.Name).Should().Contain(new[] { "élection", "économie", "sport" });

        var filtered = await (await Client().GetAsync("/api/v1/tags?q=spo")).Content.ReadFromJsonAsync<List<TagItem>>();
        filtered!.Should().OnlyContain(t => t.Name.Contains("spo"));
    }
}
