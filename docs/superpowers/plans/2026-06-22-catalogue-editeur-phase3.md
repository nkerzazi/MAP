# Phase 3 — Catalogue éditeur Implementation Plan

> **For agentic workers:** Implement task-by-task in TDD. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Rendre le catalogue réel — édition des métadonnées, publication/archivage, listing éditeur, catalogue public paginé avec recherche full-text PostgreSQL.

**Architecture:** `ICatalogService` (Application) → `CatalogService` (Infrastructure, `AppDbContext`). Colonne `SearchVector` générée par PostgreSQL (`tsvector` sur titre+description, index GIN). Contrôleur mince qui extrait l'identité (claim `sub` + rôle `Admin`) et mappe les exceptions métier en `ProblemDetails`. Propriétaire-seul (+ Admin) pour les écritures ; catalogue public anonyme.

**Tech Stack:** ASP.NET Core 8, EF Core 8 + Npgsql (tsvector généré, `websearch_to_tsquery`), xUnit + Testcontainers + WebApplicationFactory.

**Référence spec :** `docs/superpowers/specs/2026-06-22-catalogue-editeur-phase3-design.md`

---

## File Structure

**Domain** : Modify `backend/src/MediaPlatform.Domain/Entities/Video.cs` (+ `SearchVector`).
**Infrastructure** :
- Modify `backend/src/MediaPlatform.Infrastructure/Persistence/AppDbContext.cs` (config tsvector)
- Create migration sous `backend/src/MediaPlatform.Infrastructure/Persistence/Migrations/`
- Create `backend/src/MediaPlatform.Infrastructure/Catalog/CatalogService.cs`
**Application** :
- Create `backend/src/MediaPlatform.Application/Catalog/UpdateVideoRequest.cs`
- Create `backend/src/MediaPlatform.Application/Catalog/CatalogDtos.cs` (`VideoListItem`, `VideoDetail`, `PagedResult<T>`, `CatalogQuery`)
- Create `backend/src/MediaPlatform.Application/Catalog/CatalogExceptions.cs`
- Create `backend/src/MediaPlatform.Application/Interfaces/ICatalogService.cs`
**Api** :
- Modify `backend/src/MediaPlatform.Api/Controllers/VideosController.cs`
- Modify `backend/src/MediaPlatform.Api/Program.cs` (DI `ICatalogService`)
**Tests** :
- Create `backend/tests/MediaPlatform.IntegrationTests/CatalogServiceTests.cs`
- Create `backend/tests/MediaPlatform.IntegrationTests/CatalogEndpointsTests.cs`

---

## Task 1: Colonne SearchVector générée + migration

- [ ] **Step 1: Ajouter la propriété** dans `Video.cs` (après `UpdatedAt`) :
```csharp
    public NpgsqlTypes.NpgsqlTsVector SearchVector { get; set; } = null!;
```

- [ ] **Step 2: Configurer la colonne générée** dans `AppDbContext.OnModelCreating`, dans le bloc `b.Entity<Video>(e => { ... })`, ajouter avant la fin du lambda :
```csharp
            e.HasGeneratedTsVectorColumn(x => x.SearchVector, "french", x => new { x.Title, x.Description })
             .HasIndex(x => x.SearchVector).HasMethod("GIN");
```

- [ ] **Step 3: Générer la migration**. S'assurer que l'outil EF est dispo (`dotnet tool install --global dotnet-ef` si `dotnet ef` échoue), puis :
```
dotnet ef migrations add AddVideoSearchVector --project backend/src/MediaPlatform.Infrastructure --startup-project backend/src/MediaPlatform.Api
```
Vérifier que la migration générée contient une colonne `SearchVector` de type `tsvector` avec `computedColumnSql`/`stored: true` et un index GIN.

- [ ] **Step 4: Build** — `dotnet build backend/MediaPlatform.sln --nologo` → 0 erreurs.

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Domain/Entities/Video.cs backend/src/MediaPlatform.Infrastructure/Persistence
git commit -m "feat(catalog): colonne tsvector generee (recherche FT) + migration"
```

---

## Task 2: DTOs, exceptions, interface (Application)

- [ ] **Step 1: `UpdateVideoRequest.cs`**
```csharp
namespace MediaPlatform.Application.Catalog;

public record UpdateVideoRequest(string Title, string? Description, Guid? CategoryId, IReadOnlyList<string> Tags);
```

- [ ] **Step 2: `CatalogDtos.cs`**
```csharp
namespace MediaPlatform.Application.Catalog;

public record VideoListItem(Guid Id, string Title, string Slug, string? CategoryName,
    double? DurationSeconds, DateTimeOffset? PublishedAt, string Status);

public record VideoDetail(Guid Id, string Title, string? Description, string Slug, string Status,
    string? CategoryName, double? DurationSeconds, DateTimeOffset? PublishedAt,
    IReadOnlyList<string> Tags, IReadOnlyList<RenditionInfo> Renditions);

public record RenditionInfo(string Resolution, int Bitrate, string ManifestKey);

public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Critères du catalogue public.</summary>
public record CatalogQuery(string? Q, Guid? CategoryId, string? Tag, int Page = 1, int PageSize = 20);
```

- [ ] **Step 3: `CatalogExceptions.cs`**
```csharp
namespace MediaPlatform.Application.Catalog;

public class VideoNotFoundException() : Exception("Vidéo introuvable.");
public class NotVideoOwnerException() : Exception("Action réservée au propriétaire de la vidéo.");
public class InvalidVideoStateException(string detail) : Exception(detail);
public class CategoryNotFoundException(Guid id) : Exception($"Catégorie introuvable : {id}.");
```

- [ ] **Step 4: `ICatalogService.cs`**
```csharp
using MediaPlatform.Application.Catalog;

namespace MediaPlatform.Application.Interfaces;

public interface ICatalogService
{
    Task<VideoDetail> UpdateAsync(Guid id, UpdateVideoRequest request, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task PublishAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task ArchiveAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<PagedResult<VideoListItem>> GetMineAsync(Guid userId, int page, CancellationToken ct = default);
    Task<PagedResult<VideoListItem>> SearchPublishedAsync(CatalogQuery query, CancellationToken ct = default);
    Task<VideoDetail> GetDetailAsync(Guid id, Guid? userId, bool isAdmin, CancellationToken ct = default);
}
```

- [ ] **Step 5: Build** — `dotnet build backend/src/MediaPlatform.Application/MediaPlatform.Application.csproj --nologo` → 0 erreurs.

- [ ] **Step 6: Commit**
```
git add backend/src/MediaPlatform.Application/Catalog backend/src/MediaPlatform.Application/Interfaces/ICatalogService.cs
git commit -m "feat(catalog): DTOs, exceptions et interface ICatalogService"
```

---

## Task 3: CatalogService — écritures (update/publish/archive/mine) + tests

**Test:** `backend/tests/MediaPlatform.IntegrationTests/CatalogServiceTests.cs`

- [ ] **Step 1: Écrire le test** (Postgres Testcontainer). Helpers de seed + cas :
```csharp
using FluentAssertions;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Catalog;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class CatalogServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);
    private CatalogService Svc(AppDbContext db) => new(db);

    private async Task<(Guid videoId, Guid ownerId)> SeedVideo(VideoStatus status = VideoStatus.Ready)
    {
        var ownerId = Guid.NewGuid(); var videoId = Guid.NewGuid();
        await using var db = NewDb();
        db.Users.Add(new User { Id = ownerId, Email = $"o{ownerId:N}@map.ma", DisplayName = "O", PasswordHash = "x" });
        db.Videos.Add(new Video { Id = videoId, Title = "Titre", Slug = $"s-{videoId:N}", OwnerId = ownerId, Status = status });
        await db.SaveChangesAsync();
        return (videoId, ownerId);
    }

    [Fact]
    public async Task Update_by_owner_changes_metadata_and_tags()
    {
        var (vid, owner) = await SeedVideo();
        await using (var db = NewDb())
            await Svc(db).UpdateAsync(vid, new UpdateVideoRequest("Nouveau", "desc", null, new[] { "Politique", "politique", " Sport " }), owner, false);

        await using var check = NewDb();
        var v = await check.Videos.Include(x => x.Tags).ThenInclude(t => t.Tag).SingleAsync(x => x.Id == vid);
        v.Title.Should().Be("Nouveau");
        v.Tags.Select(t => t.Tag!.Name).Should().BeEquivalentTo(new[] { "Politique", "Sport" }); // dedup + trim
    }

    [Fact]
    public async Task Update_by_non_owner_throws_forbidden_but_admin_succeeds()
    {
        var (vid, _) = await SeedVideo();
        await using (var db = NewDb())
        {
            var act = async () => await Svc(db).UpdateAsync(vid, new UpdateVideoRequest("X", null, null, Array.Empty<string>()), Guid.NewGuid(), false);
            await act.Should().ThrowAsync<NotVideoOwnerException>();
        }
        await using (var db = NewDb())
            await Svc(db).UpdateAsync(vid, new UpdateVideoRequest("AdminEdit", null, null, Array.Empty<string>()), Guid.NewGuid(), true);
        await using var check = NewDb();
        (await check.Videos.SingleAsync(x => x.Id == vid)).Title.Should().Be("AdminEdit");
    }

    [Fact]
    public async Task Publish_requires_ready_and_archive_then_republish()
    {
        var (draft, draftOwner) = await SeedVideo(VideoStatus.Draft);
        await using (var db = NewDb())
        {
            var act = async () => await Svc(db).PublishAsync(draft, draftOwner, false);
            await act.Should().ThrowAsync<InvalidVideoStateException>();
        }

        var (vid, owner) = await SeedVideo(VideoStatus.Ready);
        await using (var db = NewDb()) await Svc(db).PublishAsync(vid, owner, false);
        await using (var db = NewDb())
        {
            var v = await db.Videos.SingleAsync(x => x.Id == vid);
            v.Status.Should().Be(VideoStatus.Published); v.PublishedAt.Should().NotBeNull();
        }
        await using (var db = NewDb()) await Svc(db).ArchiveAsync(vid, owner, false);
        await using (var db = NewDb()) (await db.Videos.SingleAsync(x => x.Id == vid)).Status.Should().Be(VideoStatus.Archived);
        await using (var db = NewDb()) await Svc(db).PublishAsync(vid, owner, false); // re-publish
        await using (var db = NewDb()) (await db.Videos.SingleAsync(x => x.Id == vid)).Status.Should().Be(VideoStatus.Published);
    }

    [Fact]
    public async Task GetMine_returns_owner_videos_all_statuses()
    {
        var (vid, owner) = await SeedVideo(VideoStatus.Draft);
        await using var db = NewDb();
        var page = await Svc(db).GetMineAsync(owner, 1);
        page.Items.Should().ContainSingle(i => i.Id == vid);
    }
}
```

- [ ] **Step 2: Lancer → échec** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter CatalogServiceTests --nologo` (CatalogService n'existe pas).

- [ ] **Step 3: Implémenter** `backend/src/MediaPlatform.Infrastructure/Catalog/CatalogService.cs` (méthodes d'écriture + mine ; les méthodes publiques sont complétées en Task 4) :
```csharp
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Catalog;

public class CatalogService : ICatalogService
{
    private readonly AppDbContext _db;
    public CatalogService(AppDbContext db) => _db = db;

    public async Task<VideoDetail> UpdateAsync(Guid id, UpdateVideoRequest request, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var video = await _db.Videos.Include(v => v.Tags)
            .FirstOrDefaultAsync(v => v.Id == id, ct) ?? throw new VideoNotFoundException();
        EnsureOwner(video, userId, isAdmin);

        if (request.CategoryId is { } cat && !await _db.Categories.AnyAsync(c => c.Id == cat, ct))
            throw new CategoryNotFoundException(cat);

        video.Title = request.Title;
        video.Description = request.Description;
        video.CategoryId = request.CategoryId;
        video.UpdatedAt = DateTimeOffset.UtcNow;

        await SetTagsAsync(video, request.Tags, ct);
        await _db.SaveChangesAsync(ct);
        return await GetDetailAsync(id, userId, isAdmin, ct);
    }

    public async Task PublishAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var video = await LoadOwned(id, userId, isAdmin, ct);
        if (video.Status is not (VideoStatus.Ready or VideoStatus.Archived))
            throw new InvalidVideoStateException($"Publication impossible depuis l'état {video.Status}.");
        video.Status = VideoStatus.Published;
        video.PublishedAt = DateTimeOffset.UtcNow;
        video.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ArchiveAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var video = await LoadOwned(id, userId, isAdmin, ct);
        if (video.Status != VideoStatus.Published)
            throw new InvalidVideoStateException($"Archivage impossible depuis l'état {video.Status}.");
        video.Status = VideoStatus.Archived;
        video.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<VideoListItem>> GetMineAsync(Guid userId, int page, CancellationToken ct = default)
    {
        var query = _db.Videos.Where(v => v.OwnerId == userId).OrderByDescending(v => v.UpdatedAt);
        return await PageAsync(query, page, 20, ct);
    }

    // --- complété en Task 4 ---
    public Task<PagedResult<VideoListItem>> SearchPublishedAsync(CatalogQuery query, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task<VideoDetail> GetDetailAsync(Guid id, Guid? userId, bool isAdmin, CancellationToken ct = default)
        => throw new NotImplementedException();

    // --- helpers ---
    private async Task<Video> LoadOwned(Guid id, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var video = await _db.Videos.FirstOrDefaultAsync(v => v.Id == id, ct) ?? throw new VideoNotFoundException();
        EnsureOwner(video, userId, isAdmin);
        return video;
    }

    private static void EnsureOwner(Video video, Guid userId, bool isAdmin)
    {
        if (!isAdmin && video.OwnerId != userId) throw new NotVideoOwnerException();
    }

    private async Task SetTagsAsync(Video video, IReadOnlyList<string> tagNames, CancellationToken ct)
    {
        var names = tagNames.Select(t => t.Trim()).Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        video.Tags.Clear();
        foreach (var name in names)
        {
            var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == name, ct)
                      ?? _db.Tags.Add(new Tag { Id = Guid.NewGuid(), Name = name }).Entity;
            video.Tags.Add(new VideoTag { VideoId = video.Id, TagId = tag.Id, Tag = tag });
        }
    }

    internal static async Task<PagedResult<VideoListItem>> PageAsync(IQueryable<Video> query, int page, int pageSize, CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(v => new VideoListItem(v.Id, v.Title, v.Slug, v.Category!.Name, v.DurationSeconds, v.PublishedAt, v.Status.ToString()))
            .ToListAsync(ct);
        return new PagedResult<VideoListItem>(items, total, page, pageSize);
    }
}
```
> Note : `v.Category!.Name` dans la projection produit `NULL` si pas de catégorie (LEFT JOIN EF) — pas de NRE car projection SQL.

- [ ] **Step 4: Lancer → succès** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter CatalogServiceTests --nologo` (4 tests verts).

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Infrastructure/Catalog/CatalogService.cs backend/tests/MediaPlatform.IntegrationTests/CatalogServiceTests.cs
git commit -m "feat(catalog): CatalogService ecritures (update/publish/archive/mine) + tests"
```

---

## Task 4: CatalogService — catalogue public (recherche/detail)

- [ ] **Step 1: Ajouter les tests** dans `CatalogServiceTests.cs` :
```csharp
    [Fact]
    public async Task Search_returns_only_published_and_matches_q()
    {
        var (pub, owner) = await SeedVideo(VideoStatus.Ready);
        await using (var db = NewDb())
        {
            await Svc(db).UpdateAsync(pub, new UpdateVideoRequest("Conseil de gouvernement", "reunion hebdomadaire", null, Array.Empty<string>()), owner, false);
            await Svc(db).PublishAsync(pub, owner, false);
        }
        await SeedVideo(VideoStatus.Draft); // ne doit pas apparaître

        await using var read = NewDb();
        var all = await Svc(read).SearchPublishedAsync(new CatalogQuery(null, null, null));
        all.Items.Should().ContainSingle(i => i.Id == pub);

        var hit = await Svc(read).SearchPublishedAsync(new CatalogQuery("gouvernement", null, null));
        hit.Items.Should().ContainSingle(i => i.Id == pub);

        var miss = await Svc(read).SearchPublishedAsync(new CatalogQuery("introuvable", null, null));
        miss.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDetail_hides_non_published_from_anonymous()
    {
        var (draft, owner) = await SeedVideo(VideoStatus.Draft);
        await using var db = NewDb();
        var anon = async () => await Svc(db).GetDetailAsync(draft, null, false);
        await anon.Should().ThrowAsync<VideoNotFoundException>();
        var asOwner = await Svc(db).GetDetailAsync(draft, owner, false);
        asOwner.Id.Should().Be(draft);
    }
```

- [ ] **Step 2: Lancer → échec** (NotImplementedException).

- [ ] **Step 3: Implémenter** — remplacer les deux méthodes `throw new NotImplementedException()` :
```csharp
    public async Task<PagedResult<VideoListItem>> SearchPublishedAsync(CatalogQuery query, CancellationToken ct = default)
    {
        var q = _db.Videos.Where(v => v.Status == VideoStatus.Published);

        if (query.CategoryId is { } cat) q = q.Where(v => v.CategoryId == cat);
        if (!string.IsNullOrWhiteSpace(query.Tag)) q = q.Where(v => v.Tags.Any(t => t.Tag!.Name == query.Tag));

        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var tsQuery = EF.Functions.WebSearchToTsQuery("french", query.Q);
            q = q.Where(v => v.SearchVector.Matches(tsQuery))
                 .OrderByDescending(v => v.SearchVector.Rank(tsQuery));
        }
        else
        {
            q = q.OrderByDescending(v => v.PublishedAt);
        }

        return await PageAsync(q, query.Page, query.PageSize <= 0 ? 20 : query.PageSize, ct);
    }

    public async Task<VideoDetail> GetDetailAsync(Guid id, Guid? userId, bool isAdmin, CancellationToken ct = default)
    {
        var video = await _db.Videos
            .Include(v => v.Category)
            .Include(v => v.Renditions)
            .Include(v => v.Tags).ThenInclude(t => t.Tag)
            .FirstOrDefaultAsync(v => v.Id == id, ct) ?? throw new VideoNotFoundException();

        var visible = video.Status == VideoStatus.Published || (userId is { } u && (isAdmin || video.OwnerId == u));
        if (!visible) throw new VideoNotFoundException();

        return new VideoDetail(video.Id, video.Title, video.Description, video.Slug, video.Status.ToString(),
            video.Category?.Name, video.DurationSeconds, video.PublishedAt,
            video.Tags.Select(t => t.Tag!.Name).ToList(),
            video.Renditions.Select(r => new RenditionInfo(r.Resolution, r.Bitrate, r.ManifestKey)).ToList());
    }
```

- [ ] **Step 4: Lancer → succès** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter CatalogServiceTests --nologo` (6 tests verts).

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Infrastructure/Catalog/CatalogService.cs backend/tests/MediaPlatform.IntegrationTests/CatalogServiceTests.cs
git commit -m "feat(catalog): catalogue public (recherche FT + detail) + tests"
```

---

## Task 5: Câblage VideosController + DI

- [ ] **Step 1: Enregistrer le service** dans `Program.cs`, après `builder.Services.AddScoped<ITranscodePipeline, TranscodePipeline>();` :
```csharp
builder.Services.AddScoped<ICatalogService, MediaPlatform.Infrastructure.Catalog.CatalogService>();
```

- [ ] **Step 2: Étendre `VideosController`**. Injecter `ICatalogService`, ajouter un helper d'identité, remplacer les stubs `List`/`Get` et ajouter `Update`/`Publish`/`Archive`/`Mine`. Le contrôleur conserve `Create`/`UploadChunk`/`Complete`/`Stream` (Phase 2). Remplacer le constructeur et les actions concernées :

Ajouter le champ + injection (fusionner avec le constructeur existant) :
```csharp
    private readonly ICatalogService _catalog;
    // ... ajouter ICatalogService catalog au constructeur et: _catalog = catalog;

    private Guid CurrentUserId() => Guid.Parse(User.FindFirst("sub")!.Value);
    private bool IsAdmin() => User.IsInRole("Admin");
```

Remplacer l'action `List` (stub) par :
```csharp
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] Guid? categoryId,
        [FromQuery] string? tag, [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await _catalog.SearchPublishedAsync(new MediaPlatform.Application.Catalog.CatalogQuery(q, categoryId, tag, page), ct));
```

Remplacer l'action `Get` (stub) par :
```csharp
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        Guid? uid = User.FindFirst("sub") is { } c ? Guid.Parse(c.Value) : null;
        try { return Ok(await _catalog.GetDetailAsync(id, uid, IsAdminSafe(), ct)); }
        catch (MediaPlatform.Application.Catalog.VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    private bool IsAdminSafe() => User.Identity?.IsAuthenticated == true && User.IsInRole("Admin");
```

Ajouter les actions éditeur :
```csharp
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Editeur,Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] MediaPlatform.Application.Catalog.UpdateVideoRequest req, CancellationToken ct)
        => await CatalogAction(() => _catalog.UpdateAsync(id, req, CurrentUserId(), IsAdmin(), ct));

    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = "Editeur,Admin")]
    public Task<IActionResult> Publish(Guid id, CancellationToken ct)
        => CatalogVoid(() => _catalog.PublishAsync(id, CurrentUserId(), IsAdmin(), ct));

    [HttpPost("{id:guid}/archive")]
    [Authorize(Roles = "Editeur,Admin")]
    public Task<IActionResult> Archive(Guid id, CancellationToken ct)
        => CatalogVoid(() => _catalog.ArchiveAsync(id, CurrentUserId(), IsAdmin(), ct));

    [HttpGet("mine")]
    [Authorize(Roles = "Editeur,Admin")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await _catalog.GetMineAsync(CurrentUserId(), page, ct));

    private async Task<IActionResult> CatalogAction<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (MediaPlatform.Application.Catalog.VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
        catch (MediaPlatform.Application.Catalog.NotVideoOwnerException) { return Problem(statusCode: 403, detail: "Action réservée au propriétaire."); }
        catch (MediaPlatform.Application.Catalog.CategoryNotFoundException ex) { return Problem(statusCode: 400, detail: ex.Message); }
        catch (MediaPlatform.Application.Catalog.InvalidVideoStateException ex) { return Problem(statusCode: 409, detail: ex.Message); }
    }

    private async Task<IActionResult> CatalogVoid(Func<Task> action)
    {
        try { await action(); return Ok(); }
        catch (MediaPlatform.Application.Catalog.VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
        catch (MediaPlatform.Application.Catalog.NotVideoOwnerException) { return Problem(statusCode: 403, detail: "Action réservée au propriétaire."); }
        catch (MediaPlatform.Application.Catalog.InvalidVideoStateException ex) { return Problem(statusCode: 409, detail: ex.Message); }
    }
```
Ajouter `using MediaPlatform.Application.Interfaces;` si absent (déjà présent pour `IUploadService`).

- [ ] **Step 3: Build** — `dotnet build backend/MediaPlatform.sln --nologo` → 0 erreurs.

- [ ] **Step 4: Commit**
```
git add backend/src/MediaPlatform.Api/Controllers/VideosController.cs backend/src/MediaPlatform.Api/Program.cs
git commit -m "feat(catalog): endpoints catalogue (list/get/update/publish/archive/mine) + DI"
```

---

## Task 6: Tests HTTP (WebApplicationFactory)

**Test:** `backend/tests/MediaPlatform.IntegrationTests/CatalogEndpointsTests.cs`. Réutilise `MinioFixture` et le pattern de `AuthEndpointsTests` (factory + Postgres + MinIO). Crée un éditeur (register Visiteur puis promotion rôle `Editeur` en base), publie une vidéo, et vérifie le catalogue.

- [ ] **Step 1: Écrire le test** :
```csharp
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
    public async Task Draft_detail_hidden_from_anonymous()
    {
        var (_, ownerId) = await CreateEditor("c@map.ma");
        var vid = await SeedReadyVideo(ownerId, "Brouillon"); // Ready (non publié) → caché à l'anonyme
        (await Client().GetAsync($"/api/v1/videos/{vid}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

- [ ] **Step 2: Lancer** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter CatalogEndpointsTests --nologo` → 3 verts.

- [ ] **Step 3: Suite complète** — `dotnet test backend/MediaPlatform.sln --nologo` → tout vert (smoke ffmpeg skippé).

- [ ] **Step 4: Commit**
```
git add backend/tests/MediaPlatform.IntegrationTests/CatalogEndpointsTests.cs
git commit -m "test(catalog): endpoints HTTP (publication, catalogue public, propriete)"
```

---

## Self-Review (effectuée)

- **Couverture spec :** SearchVector généré + migration (Task 1) · DTOs/exceptions/interface (Task 2) · update/publish/archive/mine + propriété (Task 3) · recherche FT + detail + visibilité (Task 4) · endpoints + extraction identité (Task 5) · tests HTTP RBAC/propriété/catalogue (Task 6). Tous les points de la spec ont une tâche.
- **Cohérence des types :** `ICatalogService` identique entre interface, impl. et appels contrôleur ; `CatalogQuery`/`PagedResult`/`VideoListItem`/`VideoDetail` partagés ; claims `sub`/`role` cohérents avec la Phase 1 (`MapInboundClaims=false`, `RoleClaimType=role` → `User.IsInRole("Admin")` fonctionne).
- **Incertitudes à lever à l'exécution :** disponibilité de `dotnet ef` (installer si besoin) ; forme exacte de la migration tsvector générée par Npgsql ; surcharge `EF.Functions.WebSearchToTsQuery`/`NpgsqlTsVector.Rank` selon la version Npgsql (adapter si la signature diffère, p. ex. `Matches` + `EF.Functions.ToTsQuery`).
- **Sécurité/altitude :** logique métier dans le service ; contrôleur mince ; aucune fuite d'existence (non-publiée → 404).
```
