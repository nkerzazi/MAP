# Phase 5 — Engagement Implementation Plan

> **For agentic workers:** Implement task-by-task in TDD. Steps use checkbox (`- [ ]`).

**Goal:** Commentaires, likes, partages pour utilisateurs authentifiés sur vidéos publiées, + compteurs d'engagement.

**Architecture:** `IEngagementService` (Application) → `EngagementService` (Infrastructure, `AppDbContext`). Contrôleur mince ; `[Authorize]` sur les écritures. Aucune migration (entités/index existants). Visibilité identique à la diffusion (Published/owner/admin).

**Référence spec :** `docs/superpowers/specs/2026-06-22-engagement-phase5-design.md`

---

## Task 1: DTOs, exceptions, interface (Application)

**Files:** `backend/src/MediaPlatform.Application/Engagement/EngagementDtos.cs`, `EngagementExceptions.cs`, `backend/src/MediaPlatform.Application/Interfaces/IEngagementService.cs`.

- [ ] **Step 1: `EngagementDtos.cs`**
```csharp
namespace MediaPlatform.Application.Engagement;

public record CreateCommentRequest(string Body);
public record ShareRequest(string Channel);
public record CommentItem(Guid Id, string Body, string AuthorDisplayName, DateTimeOffset CreatedAt);
public record EngagementSummary(int LikeCount, int CommentCount, int ShareCount, bool LikedByMe);
```

- [ ] **Step 2: `EngagementExceptions.cs`**
```csharp
namespace MediaPlatform.Application.Engagement;

public class CommentNotFoundException() : Exception("Commentaire introuvable.");
public class NotCommentAuthorException() : Exception("Action réservée à l'auteur du commentaire.");
```

- [ ] **Step 3: `IEngagementService.cs`**
```csharp
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Engagement;

namespace MediaPlatform.Application.Interfaces;

public interface IEngagementService
{
    Task<CommentItem> AddCommentAsync(Guid videoId, string body, Guid userId, CancellationToken ct = default);
    Task<PagedResult<CommentItem>> ListCommentsAsync(Guid videoId, int page, Guid? userId, bool isAdmin, CancellationToken ct = default);
    Task DeleteCommentAsync(Guid videoId, Guid commentId, Guid userId, bool isAdmin, CancellationToken ct = default);
    Task<EngagementSummary> LikeAsync(Guid videoId, Guid userId, CancellationToken ct = default);
    Task<EngagementSummary> UnlikeAsync(Guid videoId, Guid userId, CancellationToken ct = default);
    Task<int> ShareAsync(Guid videoId, string channel, Guid userId, CancellationToken ct = default);
    Task<EngagementSummary> GetSummaryAsync(Guid videoId, Guid? userId, bool isAdmin, CancellationToken ct = default);
}
```
> `PagedResult<T>` vit dans `MediaPlatform.Application.Catalog` (Phase 3) — réutilisé.

- [ ] **Step 4: Build** — `dotnet build backend/src/MediaPlatform.Application/MediaPlatform.Application.csproj --nologo` → 0 erreurs.

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Application/Engagement backend/src/MediaPlatform.Application/Interfaces/IEngagementService.cs
git commit -m "feat(engagement): DTOs, exceptions et interface IEngagementService"
```

---

## Task 2: EngagementService (Infrastructure) + tests

**Files:** `backend/src/MediaPlatform.Infrastructure/Engagement/EngagementService.cs`, `backend/tests/MediaPlatform.IntegrationTests/EngagementServiceTests.cs`.

- [ ] **Step 1: Écrire le test** :
```csharp
using FluentAssertions;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Engagement;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Engagement;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class EngagementServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);
    private EngagementService Svc(AppDbContext db) => new(db);

    private async Task<Guid> NewUser(string name)
    {
        var id = Guid.NewGuid();
        await using var db = NewDb();
        db.Users.Add(new User { Id = id, Email = $"u{id:N}@map.ma", DisplayName = name, PasswordHash = "x" });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> NewVideo(Guid owner, VideoStatus status = VideoStatus.Published)
    {
        var id = Guid.NewGuid();
        await using var db = NewDb();
        db.Videos.Add(new Video { Id = id, Title = "T", Slug = $"s-{id:N}", OwnerId = owner, Status = status });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Comment_add_list_and_delete_by_author()
    {
        var u = await NewUser("Alice");
        var vid = await NewVideo(u);
        await using (var db = NewDb()) await Svc(db).AddCommentAsync(vid, "  Super vidéo  ", u);
        await using (var db = NewDb())
        {
            var page = await Svc(db).ListCommentsAsync(vid, 1, null, false);
            page.Items.Should().ContainSingle();
            page.Items[0].Body.Should().Be("Super vidéo"); // trim
            page.Items[0].AuthorDisplayName.Should().Be("Alice");
        }
        Guid commentId;
        await using (var db = NewDb())
            commentId = (await db.Comments.SingleAsync(c => c.VideoId == vid)).Id;

        await using (var db = NewDb()) await Svc(db).DeleteCommentAsync(vid, commentId, u, false);
        await using (var db = NewDb()) (await db.Comments.CountAsync(c => c.VideoId == vid)).Should().Be(0);
    }

    [Fact]
    public async Task Delete_comment_by_non_author_forbidden_admin_ok()
    {
        var author = await NewUser("A"); var other = await NewUser("B");
        var vid = await NewVideo(author);
        await using (var db = NewDb()) await Svc(db).AddCommentAsync(vid, "x", author);
        Guid cid; await using (var db = NewDb()) cid = (await db.Comments.SingleAsync()).Id;

        await using (var db = NewDb())
        {
            var act = async () => await Svc(db).DeleteCommentAsync(vid, cid, other, false);
            await act.Should().ThrowAsync<NotCommentAuthorException>();
        }
        await using (var db = NewDb()) await Svc(db).DeleteCommentAsync(vid, cid, other, true); // admin
        await using (var db = NewDb()) (await db.Comments.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Like_is_idempotent_then_unlike()
    {
        var u = await NewUser("A");
        var vid = await NewVideo(u);
        await using (var db = NewDb()) await Svc(db).LikeAsync(vid, u);
        EngagementSummary s;
        await using (var db = NewDb()) s = await Svc(db).LikeAsync(vid, u); // re-like
        s.LikeCount.Should().Be(1); s.LikedByMe.Should().BeTrue();

        await using (var db = NewDb()) s = await Svc(db).UnlikeAsync(vid, u);
        s.LikeCount.Should().Be(0); s.LikedByMe.Should().BeFalse();
    }

    [Fact]
    public async Task Share_and_summary_aggregate()
    {
        var u = await NewUser("A");
        var vid = await NewVideo(u);
        await using (var db = NewDb()) await Svc(db).AddCommentAsync(vid, "c", u);
        await using (var db = NewDb()) await Svc(db).LikeAsync(vid, u);
        int shares; await using (var db = NewDb()) shares = await Svc(db).ShareAsync(vid, "lien", u);
        shares.Should().Be(1);

        await using var read = NewDb();
        var sum = await Svc(read).GetSummaryAsync(vid, u, false);
        sum.CommentCount.Should().Be(1); sum.LikeCount.Should().Be(1);
        sum.ShareCount.Should().Be(1); sum.LikedByMe.Should().BeTrue();
    }

    [Fact]
    public async Task Engaging_non_published_video_throws_not_found_for_other_users()
    {
        var owner = await NewUser("O"); var visitor = await NewUser("V");
        var vid = await NewVideo(owner, VideoStatus.Draft);
        await using var db = NewDb();
        var act = async () => await Svc(db).AddCommentAsync(vid, "x", visitor);
        await act.Should().ThrowAsync<VideoNotFoundException>();
    }
}
```

- [ ] **Step 2: Lancer → échec** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter EngagementServiceTests --nologo`.

- [ ] **Step 3: Implémenter** `EngagementService.cs` :
```csharp
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Engagement;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Engagement;

public class EngagementService : IEngagementService
{
    private const int PageSize = 20;
    private readonly AppDbContext _db;
    public EngagementService(AppDbContext db) => _db = db;

    public async Task<CommentItem> AddCommentAsync(Guid videoId, string body, Guid userId, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(videoId, userId, false, ct);
        var text = (body ?? string.Empty).Trim();
        if (text.Length == 0) throw new ArgumentException("Le commentaire est vide.", nameof(body));

        var comment = new Comment { Id = Guid.NewGuid(), VideoId = videoId, UserId = userId, Body = text };
        _db.Comments.Add(comment);
        await _db.SaveChangesAsync(ct);

        var name = await _db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).SingleAsync(ct);
        return new CommentItem(comment.Id, comment.Body, name, comment.CreatedAt);
    }

    public async Task<PagedResult<CommentItem>> ListCommentsAsync(Guid videoId, int page, Guid? userId, bool isAdmin, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(videoId, userId, isAdmin, ct);
        page = page < 1 ? 1 : page;
        var query = from c in _db.Comments
                    join u in _db.Users on c.UserId equals u.Id
                    where c.VideoId == videoId && !c.IsModerated
                    orderby c.CreatedAt descending
                    select new CommentItem(c.Id, c.Body, u.DisplayName, c.CreatedAt);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        return new PagedResult<CommentItem>(items, total, page, PageSize);
    }

    public async Task DeleteCommentAsync(Guid videoId, Guid commentId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.VideoId == videoId, ct)
                      ?? throw new CommentNotFoundException();
        if (!isAdmin && comment.UserId != userId) throw new NotCommentAuthorException();
        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<EngagementSummary> LikeAsync(Guid videoId, Guid userId, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(videoId, userId, false, ct);
        if (!await _db.Likes.AnyAsync(l => l.VideoId == videoId && l.UserId == userId, ct))
        {
            _db.Likes.Add(new Like { Id = Guid.NewGuid(), VideoId = videoId, UserId = userId });
            await _db.SaveChangesAsync(ct);
        }
        return await GetSummaryAsync(videoId, userId, false, ct);
    }

    public async Task<EngagementSummary> UnlikeAsync(Guid videoId, Guid userId, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(videoId, userId, false, ct);
        var like = await _db.Likes.FirstOrDefaultAsync(l => l.VideoId == videoId && l.UserId == userId, ct);
        if (like is not null) { _db.Likes.Remove(like); await _db.SaveChangesAsync(ct); }
        return await GetSummaryAsync(videoId, userId, false, ct);
    }

    public async Task<int> ShareAsync(Guid videoId, string channel, Guid userId, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(videoId, userId, false, ct);
        var ch = (channel ?? string.Empty).Trim();
        if (ch.Length == 0) throw new ArgumentException("Canal de partage vide.", nameof(channel));
        _db.Shares.Add(new Share { Id = Guid.NewGuid(), VideoId = videoId, UserId = userId, Channel = ch });
        await _db.SaveChangesAsync(ct);
        return await _db.Shares.CountAsync(s => s.VideoId == videoId, ct);
    }

    public async Task<EngagementSummary> GetSummaryAsync(Guid videoId, Guid? userId, bool isAdmin, CancellationToken ct = default)
    {
        await EnsureVisibleAsync(videoId, userId, isAdmin, ct);
        var likeCount = await _db.Likes.CountAsync(l => l.VideoId == videoId, ct);
        var commentCount = await _db.Comments.CountAsync(c => c.VideoId == videoId && !c.IsModerated, ct);
        var shareCount = await _db.Shares.CountAsync(s => s.VideoId == videoId, ct);
        var likedByMe = userId is { } u && await _db.Likes.AnyAsync(l => l.VideoId == videoId && l.UserId == u, ct);
        return new EngagementSummary(likeCount, commentCount, shareCount, likedByMe);
    }

    private async Task EnsureVisibleAsync(Guid videoId, Guid? userId, bool isAdmin, CancellationToken ct)
    {
        var video = await _db.Videos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == videoId, ct)
                    ?? throw new VideoNotFoundException();
        var visible = video.Status == VideoStatus.Published || (userId is { } u && (isAdmin || video.OwnerId == u));
        if (!visible) throw new VideoNotFoundException();
    }
}
```

- [ ] **Step 4: Lancer → succès** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter EngagementServiceTests --nologo` (5 verts).

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Infrastructure/Engagement backend/tests/MediaPlatform.IntegrationTests/EngagementServiceTests.cs
git commit -m "feat(engagement): EngagementService (commentaires/likes/partages) + tests"
```

---

## Task 3: Endpoints VideosController + DI + tests HTTP

**Files:** `VideosController.cs`, `Program.cs`, `backend/tests/MediaPlatform.IntegrationTests/EngagementEndpointsTests.cs`.

- [ ] **Step 1: DI** dans `Program.cs`, après `IStreamingService` :
```csharp
builder.Services.AddScoped<IEngagementService, MediaPlatform.Infrastructure.Engagement.EngagementService>();
```

- [ ] **Step 2: Injecter `IEngagementService _engagement`** (ajouter au constructeur de `VideosController`) et ajouter `using MediaPlatform.Application.Engagement;`. Ajouter les actions (les helpers `CurrentUserId()`, `CurrentUserIdOrNull()`, `IsAdmin()` existent déjà) :
```csharp
    // --- Engagement ---

    [HttpGet("{id:guid}/comments")]
    public async Task<IActionResult> ListComments(Guid id, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        try { return Ok(await _engagement.ListCommentsAsync(id, page, CurrentUserIdOrNull(), IsAdmin(), ct)); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    [HttpPost("{id:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateCommentRequest req, CancellationToken ct)
    {
        try { return Ok(await _engagement.AddCommentAsync(id, req.Body, CurrentUserId(), ct)); }
        catch (ArgumentException ex) { return Problem(statusCode: 400, detail: ex.Message); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    [HttpDelete("{id:guid}/comments/{commentId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(Guid id, Guid commentId, CancellationToken ct)
    {
        try { await _engagement.DeleteCommentAsync(id, commentId, CurrentUserId(), IsAdmin(), ct); return NoContent(); }
        catch (CommentNotFoundException) { return Problem(statusCode: 404, detail: "Commentaire introuvable."); }
        catch (NotCommentAuthorException) { return Problem(statusCode: 403, detail: "Action réservée à l'auteur."); }
    }

    [HttpPost("{id:guid}/likes")]
    [Authorize]
    public async Task<IActionResult> Like(Guid id, CancellationToken ct)
    {
        try { return Ok(await _engagement.LikeAsync(id, CurrentUserId(), ct)); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    [HttpDelete("{id:guid}/likes")]
    [Authorize]
    public async Task<IActionResult> Unlike(Guid id, CancellationToken ct)
    {
        try { return Ok(await _engagement.UnlikeAsync(id, CurrentUserId(), ct)); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    [HttpPost("{id:guid}/shares")]
    [Authorize]
    public async Task<IActionResult> Share(Guid id, [FromBody] ShareRequest req, CancellationToken ct)
    {
        try { return Ok(new { shareCount = await _engagement.ShareAsync(id, req.Channel, CurrentUserId(), ct) }); }
        catch (ArgumentException ex) { return Problem(statusCode: 400, detail: ex.Message); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    [HttpGet("{id:guid}/engagement")]
    public async Task<IActionResult> Engagement(Guid id, CancellationToken ct)
    {
        try { return Ok(await _engagement.GetSummaryAsync(id, CurrentUserIdOrNull(), IsAdmin(), ct)); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }
```

- [ ] **Step 3: Build** — `dotnet build backend/MediaPlatform.sln --nologo` → 0 erreurs.

- [ ] **Step 4: Test HTTP** `EngagementEndpointsTests.cs` (réutilise le pattern factory + helper éditeur/visiteur ; ici on crée un visiteur authentifié et une vidéo Published) :
```csharp
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
```

- [ ] **Step 5: Lancer** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter EngagementEndpointsTests --nologo` → 3 verts.

- [ ] **Step 6: Suite complète** — `dotnet test backend/MediaPlatform.sln --nologo` → tout vert (smoke skippé). En cas de flake Testcontainers (contention), relancer le seul projet d'intégration.

- [ ] **Step 7: Commit**
```
git add backend/src/MediaPlatform.Api backend/tests/MediaPlatform.IntegrationTests/EngagementEndpointsTests.cs
git commit -m "feat(engagement): endpoints commentaires/likes/partages/engagement + DI + tests HTTP"
```

---

## Self-Review (effectuée)

- **Couverture spec :** DTOs/exceptions/interface (Task 1) · service commentaires/likes/partages/résumé + visibilité (Task 2) · endpoints + auth + tests HTTP (Task 3). Tous les points ont une tâche.
- **Cohérence des types :** `IEngagementService` identique interface/impl/contrôleur ; `PagedResult<T>` réutilisé depuis `Catalog` ; `EngagementSummary`/`CommentItem` partagés ; claims `sub`/`role` cohérents (`CurrentUserId`/`IsAdmin` existants).
- **Incertitudes :** aucune migration (entités + index `(VideoId,UserId)` unique déjà présents) ; like idempotent géré par check-then-insert (pas de course attendue en test ; en prod, la contrainte unique protège).
- **Sécurité :** écritures `[Authorize]` ; visibilité (non-publiée → 404) ; suppression réservée auteur/Admin ; validations corps/canal.
```
