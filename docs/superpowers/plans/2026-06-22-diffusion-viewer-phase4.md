# Phase 4 — Diffusion & viewer Implementation Plan

> **For agentic workers:** Implement task-by-task in TDD. Steps use checkbox (`- [ ]`).

**Goal:** Diffusion HLS réelle — proxy de streaming API (manifeste + segments depuis MinIO), `/stream` réel, manifeste maître corrigé (RESOLUTION/CODECS), branchement du lecteur hls.js.

**Architecture:** `IStreamingService` (Application) → `StreamingService` (Infrastructure : `AppDbContext` + `IObjectStorage`). Proxy `GET /videos/{id}/hls/{**path}` streame depuis MinIO (content-type par extension, visibilité Published/owner/admin). Le fix manifeste touche le pipeline Phase 2 (`ProbeAsync` renvoie la largeur).

**Référence spec :** `docs/superpowers/specs/2026-06-22-diffusion-viewer-phase4-design.md`

---

## Task 1: Manifeste maître corrigé (ProbeAsync largeur + RESOLUTION/CODECS)

**Files:** `IObjectStorage.cs` (interface IVideoTranscoder), `FfmpegVideoTranscoder.cs`, `TranscodePipeline.cs`, `TranscodePipelineTests.cs`, `FfmpegSmokeTests.cs`.

- [ ] **Step 1: Changer la signature `ProbeAsync`** dans `backend/src/MediaPlatform.Application/Interfaces/IObjectStorage.cs` (bloc `IVideoTranscoder`) :
```csharp
    /// <summary>Sonde la largeur, la hauteur (px) et la durée (s) du fichier source.</summary>
    Task<(int Width, int Height, double DurationSeconds)> ProbeAsync(string sourcePath, CancellationToken ct = default);
```

- [ ] **Step 2: `FfmpegVideoTranscoder.ProbeAsync`** — lire width+height :
```csharp
    public async Task<(int Width, int Height, double DurationSeconds)> ProbeAsync(string sourcePath, CancellationToken ct = default)
    {
        var wh = await RunAsync("ffprobe",
            $"-v error -select_streams v:0 -show_entries stream=width,height -of csv=p=0 \"{sourcePath}\"", ct);
        var durationRaw = await RunAsync("ffprobe",
            $"-v error -show_entries format=duration -of csv=p=0 \"{sourcePath}\"", ct);

        var parts = wh.Trim().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            throw new InvalidOperationException($"ffprobe n'a pas renvoyé width,height pour « {sourcePath} ».");

        return (int.Parse(parts[0], CultureInfo.InvariantCulture),
                int.Parse(parts[1], CultureInfo.InvariantCulture),
                double.Parse(durationRaw.Trim(), CultureInfo.InvariantCulture));
    }
```
(`RunAsync` et `TranscodeRungAsync` inchangés.)

- [ ] **Step 3: `TranscodePipeline`** — utiliser la largeur et corriger le master. Remplacer la ligne `var (height, duration) = await _transcoder.ProbeAsync(...)` par :
```csharp
            var (srcWidth, height, duration) = await _transcoder.ProbeAsync(sourcePath, ct);
```
Et, dans la boucle des échelons, remplacer les deux lignes qui construisent l'entrée master :
```csharp
                var bandwidth = (rung.VideoKbps + rung.AudioKbps) * 1000;
                master.Append($"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION=x{rung.Height}\n");
                master.Append($"{rung.Name}/{playlist}\n");
```
par :
```csharp
                var bandwidth = (rung.VideoKbps + rung.AudioKbps) * 1000;
                var rungWidth = EvenWidth(srcWidth, height, rung.Height);
                master.Append($"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION={rungWidth}x{rung.Height},CODECS=\"avc1.640028,mp4a.40.2\"\n");
                master.Append($"{rung.Name}/{playlist}\n");
```
Ajouter le helper privé (par ex. après `RunAsync` n'existe pas ici — l'ajouter en bas de la classe `TranscodePipeline`) :
```csharp
    private static int EvenWidth(int srcWidth, int srcHeight, int targetHeight)
    {
        if (srcHeight <= 0) return targetHeight; // garde-fou
        var w = (int)Math.Round((double)srcWidth * targetHeight / srcHeight);
        return w % 2 == 0 ? w : w + 1;
    }
```

- [ ] **Step 4: Mettre à jour les doublures de test**.
Dans `backend/tests/MediaPlatform.IntegrationTests/TranscodePipelineTests.cs`, `FakeTranscoder.ProbeAsync` :
```csharp
        public Task<(int Width, int Height, double DurationSeconds)> ProbeAsync(string sourcePath, CancellationToken ct = default)
            => Task.FromResult((3840, 2160, 12.5));
```
et `ThrowingTranscoder.ProbeAsync` :
```csharp
        public Task<(int Width, int Height, double DurationSeconds)> ProbeAsync(string sourcePath, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
```
Dans `backend/tests/MediaPlatform.IntegrationTests/FfmpegSmokeTests.cs`, adapter l'appel :
```csharp
        var (width, height, duration) = await t.ProbeAsync(source);
        width.Should().Be(640);
        height.Should().Be(480);
        duration.Should().BeApproximately(2, 0.5);
```

- [ ] **Step 5: Ajouter une assertion master** dans `TranscodePipelineTests.Run_transcodes_to_ready_and_creates_renditions`, après la vérification de `master.m3u8` présent, charger son contenu et vérifier le format. Ajouter :
```csharp
        using var masterStream = await Storage().GetAsync("hls", $"hls/{vid}/master.m3u8");
        var masterText = await new StreamReader(masterStream).ReadToEndAsync();
        masterText.Should().Contain("RESOLUTION=").And.Contain("x360").And.Contain("CODECS=");
```

- [ ] **Step 6: Lancer** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter "TranscodePipelineTests|FfmpegSmokeTests" --nologo` → verts (smoke skippé si pas de ffmpeg).

- [ ] **Step 7: Commit**
```
git add backend/src/MediaPlatform.Application/Interfaces/IObjectStorage.cs backend/src/MediaPlatform.Infrastructure/Media backend/tests/MediaPlatform.IntegrationTests/TranscodePipelineTests.cs backend/tests/MediaPlatform.IntegrationTests/FfmpegSmokeTests.cs
git commit -m "fix(media): manifeste maitre HLS (RESOLUTION largeur + CODECS) via probe width"
```

---

## Task 2: IStreamingService + DTOs (Application)

**Files:** `backend/src/MediaPlatform.Application/Streaming/StreamingDtos.cs`, `backend/src/MediaPlatform.Application/Interfaces/IStreamingService.cs`.

- [ ] **Step 1: DTOs** `StreamingDtos.cs` :
```csharp
namespace MediaPlatform.Application.Streaming;

/// <summary>Objet HLS streamé depuis le stockage (manifeste ou segment).</summary>
public record HlsObject(Stream Content, string ContentType);
```

- [ ] **Step 2: Interface** `IStreamingService.cs` :
```csharp
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Streaming;

namespace MediaPlatform.Application.Interfaces;

public interface IStreamingService
{
    /// <summary>Renditions d'une vidéo visible (Published, ou propriétaire/Admin) ; sinon VideoNotFoundException.</summary>
    Task<IReadOnlyList<RenditionInfo>> GetRenditionsAsync(Guid id, Guid? userId, bool isAdmin, CancellationToken ct = default);

    /// <summary>Ouvre un objet HLS (manifeste/segment) après contrôle de visibilité et validation du chemin.</summary>
    Task<HlsObject> OpenHlsAsync(Guid id, string path, Guid? userId, bool isAdmin, CancellationToken ct = default);
}
```

- [ ] **Step 3: Build** — `dotnet build backend/src/MediaPlatform.Application/MediaPlatform.Application.csproj --nologo` → 0 erreurs.

- [ ] **Step 4: Commit**
```
git add backend/src/MediaPlatform.Application/Streaming backend/src/MediaPlatform.Application/Interfaces/IStreamingService.cs
git commit -m "feat(streaming): DTOs et interface IStreamingService"
```

---

## Task 3: StreamingService (Infrastructure) + tests

**Files:** `backend/src/MediaPlatform.Infrastructure/Streaming/StreamingService.cs`, `backend/tests/MediaPlatform.IntegrationTests/StreamingServiceTests.cs`.

> Note : il existe déjà un dossier `Infrastructure/Storage`. Le service de streaming va dans un nouveau dossier `Infrastructure/Streaming`.

- [ ] **Step 1: Écrire le test** `StreamingServiceTests.cs` :
```csharp
using System.Text;
using FluentAssertions;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Storage;
using MediaPlatform.Infrastructure.Streaming;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class StreamingServiceTests : IAsyncLifetime, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public StreamingServiceTests(MinioFixture minio) => _minio = minio;
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);
    private MinioObjectStorage Storage() => new(Options.Create(new MinioOptions
    { Endpoint = _minio.Endpoint, AccessKey = _minio.AccessKey, SecretKey = _minio.SecretKey, UseSsl = false }));
    private StreamingService Svc(AppDbContext db) => new(db, Storage());

    private async Task<(Guid id, Guid owner)> SeedVideoWithHls(VideoStatus status)
    {
        var owner = Guid.NewGuid(); var id = Guid.NewGuid();
        await using (var db = NewDb())
        {
            db.Users.Add(new User { Id = owner, Email = $"o{owner:N}@map.ma", DisplayName = "O", PasswordHash = "x" });
            db.Videos.Add(new Video { Id = id, Title = "T", Slug = $"s-{id:N}", OwnerId = owner, Status = status,
                Renditions = { new VideoRendition { Id = Guid.NewGuid(), Resolution = "360p", Bitrate = 800, ManifestKey = $"hls/{id}/360p/index.m3u8" } } });
            await db.SaveChangesAsync();
        }
        var storage = Storage();
        await storage.EnsureBucketAsync("hls");
        var bytes = Encoding.UTF8.GetBytes("#EXTM3U\n#EXT-X-VERSION:3\n");
        using var ms = new MemoryStream(bytes);
        await storage.PutAsync("hls", $"hls/{id}/master.m3u8", ms, bytes.Length, "application/vnd.apple.mpegurl");
        return (id, owner);
    }

    [Fact]
    public async Task OpenHls_streams_master_with_correct_content_type_for_published()
    {
        var (id, _) = await SeedVideoWithHls(VideoStatus.Published);
        await using var db = NewDb();
        var obj = await Svc(db).OpenHlsAsync(id, "master.m3u8", null, false);
        obj.ContentType.Should().Be("application/vnd.apple.mpegurl");
        (await new StreamReader(obj.Content).ReadToEndAsync()).Should().StartWith("#EXTM3U");
    }

    [Fact]
    public async Task NonPublished_is_hidden_from_anonymous_but_visible_to_owner()
    {
        var (id, owner) = await SeedVideoWithHls(VideoStatus.Ready);
        await using var db = NewDb();
        var anon = async () => await Svc(db).GetRenditionsAsync(id, null, false);
        await anon.Should().ThrowAsync<VideoNotFoundException>();
        (await Svc(db).GetRenditionsAsync(id, owner, false)).Should().ContainSingle(r => r.Resolution == "360p");
    }

    [Fact]
    public async Task OpenHls_rejects_path_traversal()
    {
        var (id, _) = await SeedVideoWithHls(VideoStatus.Published);
        await using var db = NewDb();
        var act = async () => await Svc(db).OpenHlsAsync(id, "../secret", null, false);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
```

- [ ] **Step 2: Lancer → échec** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter StreamingServiceTests --nologo`.

- [ ] **Step 3: Implémenter** `StreamingService.cs` :
```csharp
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Application.Streaming;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaPlatform.Infrastructure.Streaming;

public class StreamingService : IStreamingService
{
    private readonly AppDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly string _hlsBucket;

    public StreamingService(AppDbContext db, IObjectStorage storage)
    {
        _db = db; _storage = storage; _hlsBucket = "hls";
    }

    // Surcharge DI : récupère le bucket depuis les options.
    public StreamingService(AppDbContext db, IObjectStorage storage, IOptions<MinioOptions> minio)
    {
        _db = db; _storage = storage; _hlsBucket = minio.Value.HlsBucket;
    }

    public async Task<IReadOnlyList<RenditionInfo>> GetRenditionsAsync(Guid id, Guid? userId, bool isAdmin, CancellationToken ct = default)
    {
        var video = await _db.Videos.Include(v => v.Renditions).FirstOrDefaultAsync(v => v.Id == id, ct)
                    ?? throw new VideoNotFoundException();
        EnsureVisible(video, userId, isAdmin);
        return video.Renditions.Select(r => new RenditionInfo(r.Resolution, r.Bitrate, r.ManifestKey)).ToList();
    }

    public async Task<HlsObject> OpenHlsAsync(Guid id, string path, Guid? userId, bool isAdmin, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("..") || path.StartsWith('/'))
            throw new ArgumentException("Chemin HLS invalide.", nameof(path));

        var video = await _db.Videos.FirstOrDefaultAsync(v => v.Id == id, ct) ?? throw new VideoNotFoundException();
        EnsureVisible(video, userId, isAdmin);

        var stream = await _storage.GetAsync(_hlsBucket, $"hls/{id}/{path}", ct);
        return new HlsObject(stream, ContentTypeFor(path));
    }

    private static void EnsureVisible(Video video, Guid? userId, bool isAdmin)
    {
        var visible = video.Status == VideoStatus.Published || (userId is { } u && (isAdmin || video.OwnerId == u));
        if (!visible) throw new VideoNotFoundException();
    }

    private static string ContentTypeFor(string path) =>
        path.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ? "application/vnd.apple.mpegurl"
        : path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ? "video/mp2t"
        : "application/octet-stream";
}
```

- [ ] **Step 4: Lancer → succès** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter StreamingServiceTests --nologo` (3 verts).

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Infrastructure/Streaming backend/tests/MediaPlatform.IntegrationTests/StreamingServiceTests.cs
git commit -m "feat(streaming): StreamingService (proxy HLS + visibilite) + tests"
```

---

## Task 4: Endpoints VideosController + DI + tests HTTP

**Files:** `VideosController.cs`, `Program.cs`, `backend/tests/MediaPlatform.IntegrationTests/StreamingEndpointsTests.cs`.

- [ ] **Step 1: DI** dans `Program.cs`, après l'enregistrement de `ICatalogService` :
```csharp
builder.Services.AddScoped<IStreamingService, MediaPlatform.Infrastructure.Streaming.StreamingService>();
```

- [ ] **Step 2: Remplacer le stub `Stream`** dans `VideosController` et ajouter l'action `Hls`. Injecter `IStreamingService _streaming` (ajouter au constructeur). Remplacer l'action `Stream` existante par :
```csharp
    /// <summary>URL du manifeste HLS + variantes (consommé par le lecteur).</summary>
    [HttpGet("{id:guid}/stream")]
    public async Task<IActionResult> Stream(Guid id, CancellationToken ct)
    {
        try
        {
            var renditions = await _streaming.GetRenditionsAsync(id, CurrentUserIdOrNull(), IsAdmin(), ct);
            return Ok(new { id, manifestUrl = $"/api/v1/videos/{id}/hls/master.m3u8", renditions });
        }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }

    /// <summary>Proxy HLS : streame manifeste/segments depuis MinIO.</summary>
    [HttpGet("{id:guid}/hls/{**path}")]
    public async Task<IActionResult> Hls(Guid id, string path, CancellationToken ct)
    {
        try
        {
            var obj = await _streaming.OpenHlsAsync(id, path, CurrentUserIdOrNull(), IsAdmin(), ct);
            return File(obj.Content, obj.ContentType, enableRangeProcessing: true);
        }
        catch (ArgumentException) { return Problem(statusCode: 400, detail: "Chemin HLS invalide."); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }
```
Ajouter `using MediaPlatform.Application.Streaming;` (pour rien de plus que cohérence) et s'assurer que `VideoNotFoundException` (namespace `MediaPlatform.Application.Catalog`) est accessible — le `using MediaPlatform.Application.Catalog;` est déjà présent (Phase 3). Mettre à jour le constructeur pour injecter `IStreamingService streaming`.

- [ ] **Step 3: Build** — `dotnet build backend/MediaPlatform.sln --nologo` → 0 erreurs.

- [ ] **Step 4: Test HTTP** `StreamingEndpointsTests.cs` :
```csharp
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
```

- [ ] **Step 5: Lancer** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter StreamingEndpointsTests --nologo` → 3 verts.

- [ ] **Step 6: Suite complète** — `dotnet test backend/MediaPlatform.sln --nologo` → tout vert (smoke skippé).

- [ ] **Step 7: Commit**
```
git add backend/src/MediaPlatform.Api backend/tests/MediaPlatform.IntegrationTests/StreamingEndpointsTests.cs
git commit -m "feat(streaming): endpoints /stream + proxy /hls + DI + tests HTTP"
```

---

## Task 5: Branchement du lecteur (frontend, minimal)

**Files:** `frontend/src/app/viewer/viewer-page.component.ts`.

- [ ] **Step 1: Créer** `viewer-page.component.ts` :
```typescript
import { Component, Input, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { HlsPlayerComponent } from './hls-player.component';

/**
 * Page de visionnage : récupère l'URL du manifeste via GET /api/v1/videos/{id}/stream
 * puis délègue la lecture au composant hls.js. Branche le lecteur sur l'API de diffusion (Phase 4).
 */
@Component({
  selector: 'app-viewer-page',
  standalone: true,
  imports: [HlsPlayerComponent],
  template: `
    <app-hls-player *ngIf="manifestUrl" [manifestUrl]="manifestUrl"></app-hls-player>
  `,
})
export class ViewerPageComponent implements OnInit {
  @Input() videoId!: string;
  manifestUrl?: string;

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.http
      .get<{ manifestUrl: string }>(`/api/v1/videos/${this.videoId}/stream`)
      .subscribe((res) => (this.manifestUrl = res.manifestUrl));
  }
}
```

- [ ] **Step 2: Commit** (pas de build Angular — toolchain non confirmée)
```
git add frontend/src/app/viewer/viewer-page.component.ts
git commit -m "feat(viewer): page de visionnage branchee sur /stream (lecteur hls.js)"
```

---

## Self-Review (effectuée)

- **Couverture spec :** probe largeur + master corrigé (Task 1) · DTOs/interface (Task 2) · StreamingService + visibilité + path (Task 3) · endpoints proxy/stream + tests HTTP (Task 4) · branchement lecteur (Task 5). Tous les points de la spec ont une tâche.
- **Cohérence des types :** nouvelle signature `ProbeAsync (Width,Height,Duration)` propagée à l'impl, au pipeline et aux 3 doublures de test ; `RenditionInfo` réutilisé depuis `Catalog` ; `HlsObject` partagé service↔contrôleur ; visibilité identique à `CatalogService.GetDetailAsync`.
- **Incertitudes à lever :** la 2e surcharge de constructeur `StreamingService` sert au DI (résolution via `IOptions<MinioOptions>`) ; vérifier que le DI choisit bien le constructeur à plus d'arguments (ASP.NET prend le constructeur résoluble avec le plus de paramètres — `IOptions<MinioOptions>` est enregistré). Sinon, garder un seul constructeur prenant `IOptions<MinioOptions>` et adapter le test.
- **Sécurité :** validation du path (`..`, absolu, vide) ; non-publiée masquée (404) ; aucun secret.
```
