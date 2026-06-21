using FluentAssertions;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

/// <summary>
/// Vérifie la persistance Phase 1 contre un vrai PostgreSQL (Testcontainers) :
/// migrations, relations, contrainte d'unicité, et données de seed RBAC.
/// </summary>
public class PersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Migrations_seed_the_three_rbac_roles()
    {
        await using var db = CreateContext();

        var roles = await db.Roles.Select(r => r.Name).OrderBy(n => n).ToListAsync();

        roles.Should().BeEquivalentTo(new[] { "Admin", "Editeur", "Visiteur" });
    }

    [Fact]
    public async Task Video_persists_with_owner_category_tags_and_renditions()
    {
        var videoId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        await using (var db = CreateContext())
        {
            db.Users.Add(new User { Id = ownerId, Email = "editeur@map.ma", DisplayName = "Éditeur", PasswordHash = "x" });
            db.Categories.Add(new Category { Id = categoryId, Name = "Actualités", Slug = "actualites" });
            db.Tags.Add(new Tag { Id = tagId, Name = "politique" });
            db.Videos.Add(new Video
            {
                Id = videoId,
                Title = "Conseil de gouvernement",
                Slug = "conseil-de-gouvernement",
                OwnerId = ownerId,
                CategoryId = categoryId,
                Renditions = { new VideoRendition { Id = Guid.NewGuid(), Resolution = "720p", Bitrate = 2500, ManifestKey = $"hls/{videoId}/720p.m3u8" } },
                Tags = { new VideoTag { TagId = tagId } }
            });
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var loaded = await db.Videos
                .Include(v => v.Category)
                .Include(v => v.Renditions)
                .Include(v => v.Tags)
                .SingleAsync(v => v.Id == videoId);

            loaded.Title.Should().Be("Conseil de gouvernement");
            loaded.OwnerId.Should().Be(ownerId);
            loaded.Category!.Slug.Should().Be("actualites");
            loaded.Renditions.Should().ContainSingle(r => r.Resolution == "720p");
            loaded.Tags.Should().ContainSingle(t => t.TagId == tagId);
        }
    }

    [Fact]
    public async Task Like_is_unique_per_user_and_video()
    {
        var videoId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        await using (var db = CreateContext())
        {
            db.Users.Add(new User { Id = ownerId, Email = "visiteur@map.ma", DisplayName = "Visiteur", PasswordHash = "x" });
            db.Videos.Add(new Video { Id = videoId, Title = "Reportage", Slug = "reportage", OwnerId = ownerId });
            db.Likes.Add(new Like { Id = Guid.NewGuid(), VideoId = videoId, UserId = ownerId });
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            db.Likes.Add(new Like { Id = Guid.NewGuid(), VideoId = videoId, UserId = ownerId });
            var act = async () => await db.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }
}
