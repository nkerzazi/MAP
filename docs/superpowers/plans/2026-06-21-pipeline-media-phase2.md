# Phase 2 — Pipeline média Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permettre à un Éditeur de téléverser une vidéo en chunks, la stocker dans MinIO, puis la transcoder en HLS multi-débit via un worker Hangfire + FFmpeg jusqu'au statut `Ready`.

**Architecture:** Upload chunké proxifié par l'API (chaque chunk = un objet MinIO `originals/{videoId}/parts/{N}`), reconstitution par concaténation côté worker. Un process worker dédié (Hangfire + FFmpeg, storage PostgreSQL) télécharge/concatène la source, sonde la résolution, génère une ladder HLS adaptative sans upscaling, et écrit les `VideoRendition`. Découpage Domain/Application/Infrastructure/Api respecté ; l'API reste mince et sans état.

**Tech Stack:** ASP.NET Core 8, EF Core 8 (PostgreSQL 16), Minio .NET SDK 6, Hangfire (storage PostgreSQL), FFmpeg 6 (shell-out), xUnit + Testcontainers (PostgreSQL + MinIO).

**Référence spec :** `docs/superpowers/specs/2026-06-21-pipeline-media-phase2-design.md`

---

## File Structure

**Application** (interfaces + types purs, aucune dépendance EF/SDK) :
- Modify `backend/src/MediaPlatform.Application/Interfaces/IObjectStorage.cs` — ajoute `ListKeysAsync`, `GetToFileAsync`, `DeletePrefixAsync` à `IObjectStorage`.
- Create `backend/src/MediaPlatform.Application/Interfaces/IUploadService.cs` — service d'upload chunké (logique testable hors HTTP).
- Create `backend/src/MediaPlatform.Application/Interfaces/ITranscodePipeline.cs` — orchestration du transcodage.
- Create `backend/src/MediaPlatform.Application/Media/HlsLadder.cs` — sélection adaptative des échelons (fonction pure).
- Create `backend/src/MediaPlatform.Application/Media/TranscodingOptions.cs` — POCO de configuration.

**Infrastructure** (implémentations) :
- Create `backend/src/MediaPlatform.Infrastructure/Storage/MinioObjectStorage.cs`
- Create `backend/src/MediaPlatform.Infrastructure/Storage/MinioOptions.cs`
- Create `backend/src/MediaPlatform.Infrastructure/Media/UploadService.cs`
- Create `backend/src/MediaPlatform.Infrastructure/Media/FfmpegVideoTranscoder.cs`
- Create `backend/src/MediaPlatform.Infrastructure/Media/TranscodePipeline.cs`
- Create `backend/src/MediaPlatform.Infrastructure/Media/TranscodeVideoJob.cs`

**Api** :
- Modify `backend/src/MediaPlatform.Api/Controllers/VideosController.cs` — endpoints upload.
- Create `backend/src/MediaPlatform.Api/Controllers/Dtos/CreateVideoRequest.cs`
- Modify `backend/src/MediaPlatform.Api/Program.cs` — DI MinIO/pipeline, Hangfire client + serveur (mode `--worker`), buckets au boot.

**Infra/docs** :
- Create `backend/src/MediaPlatform.Api/Dockerfile` — image API + worker (FFmpeg).
- Modify `docker-compose.yml` — variables d'env MinIO/Hangfire pour api/worker.
- Modify `CLAUDE.md` — note : Hangfire storage = PostgreSQL.

**Tests** :
- Modify `backend/tests/MediaPlatform.IntegrationTests/MediaPlatform.IntegrationTests.csproj` — ajoute `Testcontainers.Minio`.
- Create `backend/tests/MediaPlatform.IntegrationTests/Fixtures/MinioFixture.cs`
- Create `backend/tests/MediaPlatform.IntegrationTests/MinioObjectStorageTests.cs`
- Create `backend/tests/MediaPlatform.IntegrationTests/UploadServiceTests.cs`
- Create `backend/tests/MediaPlatform.IntegrationTests/TranscodePipelineTests.cs`
- Create `backend/tests/MediaPlatform.Tests/MediaPlatform.Tests.csproj` (unit, sans conteneur)
- Create `backend/tests/MediaPlatform.Tests/HlsLadderTests.cs`

---

## Task 1: Configuration et options

**Files:**
- Create: `backend/src/MediaPlatform.Infrastructure/Storage/MinioOptions.cs`
- Create: `backend/src/MediaPlatform.Application/Media/TranscodingOptions.cs`

- [ ] **Step 1: Créer `MinioOptions`**

```csharp
namespace MediaPlatform.Infrastructure.Storage;

/// <summary>Configuration MinIO (section "Minio" de la configuration).</summary>
public class MinioOptions
{
    public string Endpoint { get; set; } = "minio:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public bool UseSsl { get; set; } = false;
    public string OriginalsBucket { get; set; } = "originals";
    public string HlsBucket { get; set; } = "hls";
}
```

- [ ] **Step 2: Créer `TranscodingOptions`**

```csharp
namespace MediaPlatform.Application.Media;

/// <summary>Paramètres du pipeline de transcodage (section "Transcoding").</summary>
public class TranscodingOptions
{
    public int SegmentSeconds { get; set; } = 6;
    public int MaxAttempts { get; set; } = 3;
    public List<LadderRung> Rungs { get; set; } = new()
    {
        new LadderRung { Name = "360p",  Height = 360,  VideoKbps = 800,  AudioKbps = 96 },
        new LadderRung { Name = "720p",  Height = 720,  VideoKbps = 2500, AudioKbps = 128 },
        new LadderRung { Name = "1080p", Height = 1080, VideoKbps = 5000, AudioKbps = 128 },
    };
}

public class LadderRung
{
    public string Name { get; set; } = string.Empty;
    public int Height { get; set; }
    public int VideoKbps { get; set; }
    public int AudioKbps { get; set; }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build backend/MediaPlatform.sln --nologo`
Expected: PASS (0 erreurs)

- [ ] **Step 4: Commit**

```bash
git add backend/src/MediaPlatform.Infrastructure/Storage/MinioOptions.cs backend/src/MediaPlatform.Application/Media/TranscodingOptions.cs
git commit -m "feat(media): options MinIO et transcodage"
```

---

## Task 2: Sélection adaptative de la ladder HLS (fonction pure)

**Files:**
- Create: `backend/src/MediaPlatform.Application/Media/HlsLadder.cs`
- Create: `backend/tests/MediaPlatform.Tests/MediaPlatform.Tests.csproj`
- Test: `backend/tests/MediaPlatform.Tests/HlsLadderTests.cs`

- [ ] **Step 1: Créer le projet de tests unitaires**

`backend/tests/MediaPlatform.Tests/MediaPlatform.Tests.csproj` :

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
    <PackageReference Include="FluentAssertions" Version="6.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\MediaPlatform.Application\MediaPlatform.Application.csproj" />
  </ItemGroup>
</Project>
```

Puis l'ajouter à la solution :

```bash
dotnet sln backend/MediaPlatform.sln add backend/tests/MediaPlatform.Tests/MediaPlatform.Tests.csproj
```

- [ ] **Step 2: Écrire le test qui échoue**

`backend/tests/MediaPlatform.Tests/HlsLadderTests.cs` :

```csharp
using FluentAssertions;
using MediaPlatform.Application.Media;
using Xunit;

namespace MediaPlatform.Tests;

public class HlsLadderTests
{
    private static readonly TranscodingOptions Opts = new();

    [Fact]
    public void Source_1080p_or_more_selects_all_three_rungs()
    {
        var rungs = HlsLadder.Select(sourceHeight: 2160, Opts);
        rungs.Select(r => r.Name).Should().Equal("360p", "720p", "1080p");
    }

    [Fact]
    public void Source_480p_selects_only_360p()
    {
        var rungs = HlsLadder.Select(sourceHeight: 480, Opts);
        rungs.Select(r => r.Name).Should().Equal("360p");
    }

    [Fact]
    public void Source_below_360p_still_selects_lowest_rung()
    {
        var rungs = HlsLadder.Select(sourceHeight: 240, Opts);
        rungs.Select(r => r.Name).Should().Equal("360p");
    }

    [Fact]
    public void Source_720p_selects_360_and_720()
    {
        var rungs = HlsLadder.Select(sourceHeight: 720, Opts);
        rungs.Select(r => r.Name).Should().Equal("360p", "720p");
    }
}
```

- [ ] **Step 3: Lancer le test → échec**

Run: `dotnet test backend/tests/MediaPlatform.Tests --nologo`
Expected: FAIL (le type `HlsLadder` n'existe pas → erreur de compilation)

- [ ] **Step 4: Implémenter `HlsLadder`**

`backend/src/MediaPlatform.Application/Media/HlsLadder.cs` :

```csharp
namespace MediaPlatform.Application.Media;

/// <summary>Sélection adaptative des échelons HLS : on ne transcode jamais au-dessus
/// de la résolution source (pas d'upscaling), mais on garde toujours au moins le plus bas.</summary>
public static class HlsLadder
{
    public static IReadOnlyList<LadderRung> Select(int sourceHeight, TranscodingOptions options)
    {
        var ordered = options.Rungs.OrderBy(r => r.Height).ToList();
        var applicable = ordered.Where(r => r.Height <= sourceHeight).ToList();
        return applicable.Count > 0 ? applicable : new List<LadderRung> { ordered.First() };
    }
}
```

- [ ] **Step 5: Lancer le test → succès**

Run: `dotnet test backend/tests/MediaPlatform.Tests --nologo`
Expected: PASS (4 tests verts)

- [ ] **Step 6: Commit**

```bash
git add backend/src/MediaPlatform.Application/Media/HlsLadder.cs backend/tests/MediaPlatform.Tests backend/MediaPlatform.sln
git commit -m "feat(media): selection adaptative de la ladder HLS + tests"
```

---

## Task 3: Étendre `IObjectStorage` et implémenter `MinioObjectStorage`

**Files:**
- Modify: `backend/src/MediaPlatform.Application/Interfaces/IObjectStorage.cs`
- Create: `backend/src/MediaPlatform.Infrastructure/Storage/MinioObjectStorage.cs`
- Modify: `backend/tests/MediaPlatform.IntegrationTests/MediaPlatform.IntegrationTests.csproj`
- Create: `backend/tests/MediaPlatform.IntegrationTests/Fixtures/MinioFixture.cs`
- Test: `backend/tests/MediaPlatform.IntegrationTests/MinioObjectStorageTests.cs`

- [ ] **Step 1: Étendre l'interface `IObjectStorage`**

Remplacer le contenu de `IObjectStorage` (garder `IVideoTranscoder`/`RenditionResult` inchangés en dessous) :

```csharp
namespace MediaPlatform.Application.Interfaces;

/// <summary>Abstraction du stockage objet (implémentée par MinIO).</summary>
public interface IObjectStorage
{
    Task EnsureBucketAsync(string bucket, CancellationToken ct = default);
    Task PutAsync(string bucket, string key, Stream content, long size, string contentType, CancellationToken ct = default);
    Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default);

    /// <summary>Liste les clés sous un préfixe (récursif), triées par ordre lexicographique.</summary>
    Task<IReadOnlyList<string>> ListKeysAsync(string bucket, string prefix, CancellationToken ct = default);

    /// <summary>Télécharge un objet vers un fichier local.</summary>
    Task GetToFileAsync(string bucket, string key, string destPath, CancellationToken ct = default);

    /// <summary>Supprime tous les objets sous un préfixe.</summary>
    Task DeletePrefixAsync(string bucket, string prefix, CancellationToken ct = default);

    /// <summary>URL présignée pour la diffusion HLS (consommée par le lecteur en Phase 4).</summary>
    Task<string> GetPresignedUrlAsync(string bucket, string key, TimeSpan expiry, CancellationToken ct = default);
}
```

> Note : la signature `PutAsync` gagne un paramètre `long size` (MinIO exige la taille de l'objet).

- [ ] **Step 2: Ajouter le package Testcontainers.Minio au projet de tests**

Dans `MediaPlatform.IntegrationTests.csproj`, ajouter dans le `<ItemGroup>` des packages :

```xml
    <PackageReference Include="Testcontainers.Minio" Version="3.*" />
```

- [ ] **Step 3: Créer la fixture MinIO**

`backend/tests/MediaPlatform.IntegrationTests/Fixtures/MinioFixture.cs` :

```csharp
using Minio;
using Testcontainers.Minio;
using Xunit;

namespace MediaPlatform.IntegrationTests.Fixtures;

/// <summary>Démarre un conteneur MinIO partagé pour les tests de stockage.</summary>
public sealed class MinioFixture : IAsyncLifetime
{
    private readonly MinioContainer _container = new MinioBuilder()
        .WithImage("minio/minio:latest")
        .Build();

    public string Endpoint => _container.GetConnectionString().Replace("http://", "");
    public string AccessKey => "minioadmin";
    public string SecretKey => "minioadmin";

    public IMinioClient CreateClient() =>
        new MinioClient().WithEndpoint(Endpoint).WithCredentials(AccessKey, SecretKey).Build();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
```

> Vérifier après `dotnet build` que `MinioBuilder` expose bien les identifiants par défaut `minioadmin/minioadmin` (défaut du module Testcontainers.Minio). Si le module impose d'autres identifiants, lire `_container.GetAccessKey()/GetSecretKey()` si disponibles et adapter `AccessKey`/`SecretKey`.

- [ ] **Step 4: Écrire le test qui échoue**

`backend/tests/MediaPlatform.IntegrationTests/MinioObjectStorageTests.cs` :

```csharp
using System.Text;
using FluentAssertions;
using MediaPlatform.Infrastructure.Storage;
using MediaPlatform.IntegrationTests.Fixtures;
using Microsoft.Extensions.Options;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class MinioObjectStorageTests : IClassFixture<MinioFixture>
{
    private readonly MinioObjectStorage _storage;
    private const string Bucket = "originals";

    public MinioObjectStorageTests(MinioFixture fx)
    {
        var opts = Options.Create(new MinioOptions
        {
            Endpoint = fx.Endpoint, AccessKey = fx.AccessKey, SecretKey = fx.SecretKey, UseSsl = false
        });
        _storage = new MinioObjectStorage(opts);
    }

    [Fact]
    public async Task Put_then_list_and_get_roundtrips()
    {
        await _storage.EnsureBucketAsync(Bucket);
        var key = $"originals/{Guid.NewGuid()}/parts/000000";
        var bytes = Encoding.UTF8.GetBytes("hello-chunk");
        using (var ms = new MemoryStream(bytes))
            await _storage.PutAsync(Bucket, key, ms, bytes.Length, "application/octet-stream");

        var keys = await _storage.ListKeysAsync(Bucket, key[..key.LastIndexOf('/')] + "/");
        keys.Should().ContainSingle().Which.Should().Be(key);

        using var got = await _storage.GetAsync(Bucket, key);
        using var reader = new StreamReader(got);
        (await reader.ReadToEndAsync()).Should().Be("hello-chunk");
    }

    [Fact]
    public async Task DeletePrefix_removes_all_objects_under_prefix()
    {
        await _storage.EnsureBucketAsync(Bucket);
        var prefix = $"originals/{Guid.NewGuid()}/parts/";
        foreach (var i in Enumerable.Range(0, 3))
        {
            var b = Encoding.UTF8.GetBytes($"part{i}");
            using var ms = new MemoryStream(b);
            await _storage.PutAsync(Bucket, $"{prefix}{i:000000}", ms, b.Length, "application/octet-stream");
        }

        await _storage.DeletePrefixAsync(Bucket, prefix);

        (await _storage.ListKeysAsync(Bucket, prefix)).Should().BeEmpty();
    }
}
```

- [ ] **Step 5: Lancer → échec**

Run: `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter MinioObjectStorageTests --nologo`
Expected: FAIL (`MinioObjectStorage` n'existe pas)

- [ ] **Step 6: Implémenter `MinioObjectStorage`**

`backend/src/MediaPlatform.Infrastructure/Storage/MinioObjectStorage.cs` :

```csharp
using MediaPlatform.Application.Interfaces;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace MediaPlatform.Infrastructure.Storage;

/// <summary>Implémentation MinIO d'<see cref="IObjectStorage"/> (SDK Minio .NET 6).</summary>
public class MinioObjectStorage : IObjectStorage
{
    private readonly IMinioClient _client;

    public MinioObjectStorage(IOptions<MinioOptions> options)
    {
        var o = options.Value;
        _client = new MinioClient()
            .WithEndpoint(o.Endpoint)
            .WithCredentials(o.AccessKey, o.SecretKey)
            .WithSSL(o.UseSsl)
            .Build();
    }

    public async Task EnsureBucketAsync(string bucket, CancellationToken ct = default)
    {
        var exists = await _client.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct);
        if (!exists)
            await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
    }

    public Task PutAsync(string bucket, string key, Stream content, long size, string contentType, CancellationToken ct = default) =>
        _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(bucket).WithObject(key)
            .WithStreamData(content).WithObjectSize(size)
            .WithContentType(contentType), ct);

    public async Task<Stream> GetAsync(string bucket, string key, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket).WithObject(key)
            .WithCallbackStream(async (s, c) => await s.CopyToAsync(ms, c)), ct);
        ms.Position = 0;
        return ms;
    }

    public async Task GetToFileAsync(string bucket, string key, string destPath, CancellationToken ct = default)
    {
        await using var file = File.Create(destPath);
        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(bucket).WithObject(key)
            .WithCallbackStream(async (s, c) => await s.CopyToAsync(file, c)), ct);
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(string bucket, string prefix, CancellationToken ct = default)
    {
        var keys = new List<string>();
        var args = new ListObjectsArgs().WithBucket(bucket).WithPrefix(prefix).WithRecursive(true);
        await foreach (var item in _client.ListObjectsEnumAsync(args, ct))
            keys.Add(item.Key);
        keys.Sort(StringComparer.Ordinal);
        return keys;
    }

    public async Task DeletePrefixAsync(string bucket, string prefix, CancellationToken ct = default)
    {
        foreach (var key in await ListKeysAsync(bucket, prefix, ct))
            await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(bucket).WithObject(key), ct);
    }

    public Task<string> GetPresignedUrlAsync(string bucket, string key, TimeSpan expiry, CancellationToken ct = default) =>
        _client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket(bucket).WithObject(key).WithExpiry((int)expiry.TotalSeconds));
}
```

- [ ] **Step 7: Lancer → succès**

Run: `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter MinioObjectStorageTests --nologo`
Expected: PASS (2 tests verts)

- [ ] **Step 8: Commit**

```bash
git add backend/src/MediaPlatform.Application/Interfaces/IObjectStorage.cs backend/src/MediaPlatform.Infrastructure/Storage/MinioObjectStorage.cs backend/tests/MediaPlatform.IntegrationTests
git commit -m "feat(media): MinioObjectStorage + tests Testcontainers"
```

---

## Task 4: Service d'upload chunké

**Files:**
- Create: `backend/src/MediaPlatform.Application/Interfaces/IUploadService.cs`
- Create: `backend/src/MediaPlatform.Infrastructure/Media/UploadService.cs`
- Test: `backend/tests/MediaPlatform.IntegrationTests/UploadServiceTests.cs`

- [ ] **Step 1: Définir l'interface `IUploadService`**

`backend/src/MediaPlatform.Application/Interfaces/IUploadService.cs` :

```csharp
namespace MediaPlatform.Application.Interfaces;

/// <summary>Upload chunké d'une source vidéo vers le stockage objet.</summary>
public interface IUploadService
{
    /// <summary>Stocke un chunk (idempotent). Retourne les index déjà reçus pour la vidéo.</summary>
    Task<IReadOnlyList<int>> StoreChunkAsync(Guid videoId, int index, Stream content, long size, CancellationToken ct = default);

    /// <summary>Vérifie que les <paramref name="total"/> chunks sont présents (0..total-1).
    /// Retourne le préfixe des parts à utiliser comme OriginalKey.</summary>
    Task<string> CompleteAsync(Guid videoId, int total, CancellationToken ct = default);

    /// <summary>Convention de nommage des clés (exposée pour le worker).</summary>
    static string PartsPrefix(Guid videoId) => $"originals/{videoId}/parts/";
    static string PartKey(Guid videoId, int index) => $"originals/{videoId}/parts/{index:000000}";
}
```

- [ ] **Step 2: Écrire le test qui échoue**

`backend/tests/MediaPlatform.IntegrationTests/UploadServiceTests.cs` :

```csharp
using System.Text;
using FluentAssertions;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Infrastructure.Media;
using MediaPlatform.Infrastructure.Storage;
using MediaPlatform.IntegrationTests.Fixtures;
using Microsoft.Extensions.Options;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class UploadServiceTests : IClassFixture<MinioFixture>
{
    private readonly UploadService _svc;

    public UploadServiceTests(MinioFixture fx)
    {
        var storage = new MinioObjectStorage(Options.Create(new MinioOptions
        {
            Endpoint = fx.Endpoint, AccessKey = fx.AccessKey, SecretKey = fx.SecretKey, UseSsl = false
        }));
        _svc = new UploadService(storage, Options.Create(new MinioOptions()));
    }

    private static Stream Chunk(string s) => new MemoryStream(Encoding.UTF8.GetBytes(s));

    [Fact]
    public async Task StoreChunk_is_idempotent_and_tracks_indices()
    {
        var id = Guid.NewGuid();
        await _svc.StoreChunkAsync(id, 0, Chunk("aaa"), 3);
        await _svc.StoreChunkAsync(id, 1, Chunk("bbb"), 3);
        var received = await _svc.StoreChunkAsync(id, 1, Chunk("bbb"), 3); // re-post

        received.Should().Equal(0, 1);
    }

    [Fact]
    public async Task Complete_throws_when_a_chunk_is_missing()
    {
        var id = Guid.NewGuid();
        await _svc.StoreChunkAsync(id, 0, Chunk("aaa"), 3);
        var act = async () => await _svc.CompleteAsync(id, total: 2);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Complete_returns_parts_prefix_when_all_present()
    {
        var id = Guid.NewGuid();
        await _svc.StoreChunkAsync(id, 0, Chunk("aaa"), 3);
        await _svc.StoreChunkAsync(id, 1, Chunk("bbb"), 3);
        (await _svc.CompleteAsync(id, total: 2)).Should().Be(IUploadService.PartsPrefix(id));
    }
}
```

- [ ] **Step 3: Lancer → échec**

Run: `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter UploadServiceTests --nologo`
Expected: FAIL (`UploadService` n'existe pas)

- [ ] **Step 4: Implémenter `UploadService`**

`backend/src/MediaPlatform.Infrastructure/Media/UploadService.cs` :

```csharp
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace MediaPlatform.Infrastructure.Media;

public class UploadService : IUploadService
{
    private readonly IObjectStorage _storage;
    private readonly string _bucket;

    public UploadService(IObjectStorage storage, IOptions<MinioOptions> minio)
    {
        _storage = storage;
        _bucket = minio.Value.OriginalsBucket;
    }

    public async Task<IReadOnlyList<int>> StoreChunkAsync(Guid videoId, int index, Stream content, long size, CancellationToken ct = default)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index));
        await _storage.EnsureBucketAsync(_bucket, ct);
        await _storage.PutAsync(_bucket, IUploadService.PartKey(videoId, index), content, size, "application/octet-stream", ct);
        return await ReceivedIndicesAsync(videoId, ct);
    }

    public async Task<string> CompleteAsync(Guid videoId, int total, CancellationToken ct = default)
    {
        if (total <= 0) throw new ArgumentOutOfRangeException(nameof(total));
        var received = (await ReceivedIndicesAsync(videoId, ct)).ToHashSet();
        for (var i = 0; i < total; i++)
            if (!received.Contains(i))
                throw new InvalidOperationException($"Chunk {i} manquant pour la vidéo {videoId}.");
        return IUploadService.PartsPrefix(videoId);
    }

    private async Task<IReadOnlyList<int>> ReceivedIndicesAsync(Guid videoId, CancellationToken ct)
    {
        var keys = await _storage.ListKeysAsync(_bucket, IUploadService.PartsPrefix(videoId), ct);
        return keys
            .Select(k => k[(k.LastIndexOf('/') + 1)..])
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .OrderBy(i => i)
            .ToList();
    }
}
```

- [ ] **Step 5: Lancer → succès**

Run: `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter UploadServiceTests --nologo`
Expected: PASS (3 tests verts)

- [ ] **Step 6: Commit**

```bash
git add backend/src/MediaPlatform.Application/Interfaces/IUploadService.cs backend/src/MediaPlatform.Infrastructure/Media/UploadService.cs backend/tests/MediaPlatform.IntegrationTests/UploadServiceTests.cs
git commit -m "feat(media): service d'upload chunke + tests"
```

---

## Task 5: Transcodeur FFmpeg (shell-out)

**Files:**
- Create: `backend/src/MediaPlatform.Infrastructure/Media/FfmpegVideoTranscoder.cs`
- Test: étendre `backend/tests/MediaPlatform.IntegrationTests/TranscodePipelineTests.cs` (test e2e gated, Task 6)

> `IVideoTranscoder` est déjà déclaré dans `IObjectStorage.cs`. On l'implémente ici. La signature actuelle `TranscodeToHlsAsync(string sourceKey, Guid videoId, ...)` suppose que le transcodeur connaît le stockage ; on la fait évoluer pour découpler : le transcodeur travaille sur un **fichier local** et écrit dans un **dossier local**, le pipeline (Task 6) gère MinIO.

- [ ] **Step 1: Faire évoluer l'interface `IVideoTranscoder`**

Dans `backend/src/MediaPlatform.Application/Interfaces/IObjectStorage.cs`, remplacer le bloc `IVideoTranscoder`/`RenditionResult` :

```csharp
namespace MediaPlatform.Application.Interfaces;

/// <summary>Transcodage FFmpeg → HLS, travaillant sur le système de fichiers local.</summary>
public interface IVideoTranscoder
{
    /// <summary>Sonde la hauteur (px) et la durée (s) du fichier source.</summary>
    Task<(int Height, double DurationSeconds)> ProbeAsync(string sourcePath, CancellationToken ct = default);

    /// <summary>Transcode <paramref name="sourcePath"/> vers un échelon HLS dans <paramref name="outDir"/>
    /// (génère index.m3u8 + segments). Retourne le nom du fichier playlist relatif.</summary>
    Task<string> TranscodeRungAsync(string sourcePath, string outDir, int height, int videoKbps, int audioKbps, int segmentSeconds, CancellationToken ct = default);
}
```

> `RenditionResult` est supprimé (non utilisé ailleurs ; le pipeline crée directement les entités `VideoRendition`).

- [ ] **Step 2: Implémenter `FfmpegVideoTranscoder`**

`backend/src/MediaPlatform.Infrastructure/Media/FfmpegVideoTranscoder.cs` :

```csharp
using System.Diagnostics;
using System.Globalization;
using MediaPlatform.Application.Interfaces;

namespace MediaPlatform.Infrastructure.Media;

/// <summary>Transcodeur basé sur les binaires ffmpeg/ffprobe (présents dans l'image worker).</summary>
public class FfmpegVideoTranscoder : IVideoTranscoder
{
    public async Task<(int Height, double DurationSeconds)> ProbeAsync(string sourcePath, CancellationToken ct = default)
    {
        var height = await RunAsync("ffprobe",
            $"-v error -select_streams v:0 -show_entries stream=height -of csv=p=0 \"{sourcePath}\"", ct);
        var duration = await RunAsync("ffprobe",
            $"-v error -show_entries format=duration -of csv=p=0 \"{sourcePath}\"", ct);
        return (int.Parse(height.Trim()),
                double.Parse(duration.Trim(), CultureInfo.InvariantCulture));
    }

    public async Task<string> TranscodeRungAsync(string sourcePath, string outDir, int height, int videoKbps,
        int audioKbps, int segmentSeconds, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outDir);
        var playlist = "index.m3u8";
        var args =
            $"-y -i \"{sourcePath}\" -vf scale=-2:{height} -c:v libx264 -profile:v main -preset veryfast " +
            $"-b:v {videoKbps}k -maxrate {(int)(videoKbps * 1.07)}k -bufsize {videoKbps * 2}k " +
            $"-c:a aac -b:a {audioKbps}k -hls_time {segmentSeconds} -hls_playlist_type vod " +
            $"-hls_segment_filename \"{Path.Combine(outDir, "seg_%03d.ts")}\" \"{Path.Combine(outDir, playlist)}\"";
        await RunAsync("ffmpeg", args, ct);
        return playlist;
    }

    private static async Task<string> RunAsync(string file, string args, CancellationToken ct)
    {
        using var p = new Process
        {
            StartInfo = new ProcessStartInfo(file, args)
            {
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true
            }
        };
        p.Start();
        var stdout = await p.StandardOutput.ReadToEndAsync(ct);
        var stderr = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{file} a échoué (code {p.ExitCode}) : {stderr}");
        return stdout;
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build backend/MediaPlatform.sln --nologo`
Expected: PASS (la suppression de `RenditionResult` ne casse rien — vérifier qu'aucune autre référence n'existe : `grep -r RenditionResult backend/src` doit ne plus rien retourner)

- [ ] **Step 4: Commit**

```bash
git add backend/src/MediaPlatform.Application/Interfaces/IObjectStorage.cs backend/src/MediaPlatform.Infrastructure/Media/FfmpegVideoTranscoder.cs
git commit -m "feat(media): transcodeur FFmpeg (probe + rung HLS)"
```

---

## Task 6: Pipeline de transcodage (orchestration)

**Files:**
- Create: `backend/src/MediaPlatform.Application/Interfaces/ITranscodePipeline.cs`
- Create: `backend/src/MediaPlatform.Infrastructure/Media/TranscodePipeline.cs`
- Test: `backend/tests/MediaPlatform.IntegrationTests/TranscodePipelineTests.cs`

- [ ] **Step 1: Définir `ITranscodePipeline`**

`backend/src/MediaPlatform.Application/Interfaces/ITranscodePipeline.cs` :

```csharp
namespace MediaPlatform.Application.Interfaces;

/// <summary>Orchestration complète du transcodage d'une vidéo (appelée par le job Hangfire).</summary>
public interface ITranscodePipeline
{
    Task RunAsync(Guid videoId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Écrire le test qui échoue (fake transcoder + Postgres + MinIO)**

`backend/tests/MediaPlatform.IntegrationTests/TranscodePipelineTests.cs` :

```csharp
using FluentAssertions;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Application.Media;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Media;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.Infrastructure.Storage;
using MediaPlatform.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class TranscodePipelineTests : IAsyncLifetime, IClassFixture<MinioFixture>
{
    private readonly MinioFixture _minio;
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();

    public TranscodePipelineTests(MinioFixture minio) => _minio = minio;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        await using var db = NewDb();
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();

    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(_pg.GetConnectionString()).Options);

    private MinioObjectStorage Storage() => new(Options.Create(new MinioOptions
    { Endpoint = _minio.Endpoint, AccessKey = _minio.AccessKey, SecretKey = _minio.SecretKey, UseSsl = false }));

    /// <summary>Transcodeur factice : source "haute" (height 4000), produit une playlist vide.</summary>
    private sealed class FakeTranscoder : IVideoTranscoder
    {
        public Task<(int Height, double DurationSeconds)> ProbeAsync(string sourcePath, CancellationToken ct = default)
            => Task.FromResult((4000, 12.5));
        public Task<string> TranscodeRungAsync(string sourcePath, string outDir, int height, int v, int a, int seg, CancellationToken ct = default)
        {
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "index.m3u8"), "#EXTM3U");
            File.WriteAllText(Path.Combine(outDir, "seg_000.ts"), "x");
            return Task.FromResult("index.m3u8");
        }
    }

    private async Task<Guid> SeedVideoWithParts()
    {
        var owner = Guid.NewGuid(); var vid = Guid.NewGuid();
        await using (var db = NewDb())
        {
            db.Users.Add(new User { Id = owner, Email = $"e{owner:N}@map.ma", DisplayName = "E", PasswordHash = "x" });
            db.Videos.Add(new Video { Id = vid, Title = "T", Slug = $"t-{vid:N}", OwnerId = owner, Status = VideoStatus.Processing });
            await db.SaveChangesAsync();
        }
        var storage = Storage();
        await storage.EnsureBucketAsync("originals");
        var b = Encoding.UTF8.GetBytes("sourcebytes");
        using var ms = new MemoryStream(b);
        await storage.PutAsync("originals", IUploadService.PartKey(vid, 0), ms, b.Length, "application/octet-stream");
        return vid;
    }

    private TranscodePipeline NewPipeline(IVideoTranscoder transcoder) =>
        new(NewDb(), Storage(), transcoder, Options.Create(new MinioOptions()), Options.Create(new TranscodingOptions()));

    [Fact]
    public async Task Run_transcodes_to_ready_and_creates_renditions()
    {
        var vid = await SeedVideoWithParts();
        await NewPipeline(new FakeTranscoder()).RunAsync(vid);

        await using var db = NewDb();
        var video = await db.Videos.Include(v => v.Renditions).SingleAsync(v => v.Id == vid);
        video.Status.Should().Be(VideoStatus.Ready);
        video.DurationSeconds.Should().Be(12.5);
        video.Renditions.Select(r => r.Resolution).Should().BeEquivalentTo(new[] { "360p", "720p", "1080p" });
        (await Storage().ListKeysAsync("hls", $"hls/{vid}/")).Should().Contain($"hls/{vid}/master.m3u8");
        (await Storage().ListKeysAsync("originals", IUploadService.PartsPrefix(vid))).Should().BeEmpty(); // parts nettoyées
    }

    private sealed class ThrowingTranscoder : IVideoTranscoder
    {
        public Task<(int Height, double DurationSeconds)> ProbeAsync(string sourcePath, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
        public Task<string> TranscodeRungAsync(string s, string o, int h, int v, int a, int seg, CancellationToken ct = default)
            => throw new InvalidOperationException("boom");
    }

    [Fact]
    public async Task Run_marks_failed_when_transcoder_throws()
    {
        var vid = await SeedVideoWithParts();
        var act = async () => await NewPipeline(new ThrowingTranscoder()).RunAsync(vid);
        await act.Should().ThrowAsync<Exception>();

        await using var db = NewDb();
        (await db.Videos.SingleAsync(v => v.Id == vid)).Status.Should().Be(VideoStatus.Failed);
        (await db.TranscodeJobs.SingleAsync(j => j.VideoId == vid)).Status.Should().Be(TranscodeJobStatus.Failed);
    }
}
```

> Note : corriger la classe `ThrowingTranscoder` pour n'avoir qu'une seule implémentation de `ProbeAsync` (voir Step 4 : on n'a besoin que des deux méthodes de l'interface). Le double déclarée ci-dessus est volontairement à nettoyer lors de l'écriture — garder uniquement les méthodes publiques `ProbeAsync` et `TranscodeRungAsync` qui lèvent.

- [ ] **Step 3: Lancer → échec**

Run: `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter TranscodePipelineTests --nologo`
Expected: FAIL (`TranscodePipeline` n'existe pas)

- [ ] **Step 4: Implémenter `TranscodePipeline`**

`backend/src/MediaPlatform.Infrastructure/Media/TranscodePipeline.cs` :

```csharp
using System.Text;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Application.Media;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MediaPlatform.Infrastructure.Media;

public class TranscodePipeline : ITranscodePipeline
{
    private readonly AppDbContext _db;
    private readonly IObjectStorage _storage;
    private readonly IVideoTranscoder _transcoder;
    private readonly MinioOptions _minio;
    private readonly TranscodingOptions _opts;

    public TranscodePipeline(AppDbContext db, IObjectStorage storage, IVideoTranscoder transcoder,
        IOptions<MinioOptions> minio, IOptions<TranscodingOptions> opts)
    {
        _db = db; _storage = storage; _transcoder = transcoder;
        _minio = minio.Value; _opts = opts.Value;
    }

    public async Task RunAsync(Guid videoId, CancellationToken ct = default)
    {
        var video = await _db.Videos.SingleAsync(v => v.Id == videoId, ct);
        var job = await _db.TranscodeJobs.FirstOrDefaultAsync(j => j.VideoId == videoId, ct);
        if (job is null)
        {
            job = new TranscodeJob { Id = Guid.NewGuid(), VideoId = videoId, CreatedAt = DateTimeOffset.UtcNow };
            _db.TranscodeJobs.Add(job);
        }
        job.Status = TranscodeJobStatus.Running;
        job.Attempts += 1;
        video.Status = VideoStatus.Processing;
        await _db.SaveChangesAsync(ct);

        var work = Directory.CreateTempSubdirectory("map-transcode-");
        try
        {
            // 1. Télécharger + concaténer les parts dans l'ordre.
            var sourcePath = Path.Combine(work.FullName, "source.bin");
            var parts = await _storage.ListKeysAsync(_minio.OriginalsBucket, IUploadService.PartsPrefix(videoId), ct);
            await using (var src = File.Create(sourcePath))
            {
                foreach (var key in parts) // ListKeysAsync trie en ordinal => ordre des index
                {
                    var partPath = Path.Combine(work.FullName, "part.tmp");
                    await _storage.GetToFileAsync(_minio.OriginalsBucket, key, partPath, ct);
                    await using var pin = File.OpenRead(partPath);
                    await pin.CopyToAsync(src, ct);
                }
            }

            // 2. Sonder + sélectionner la ladder.
            var (height, duration) = await _transcoder.ProbeAsync(sourcePath, ct);
            var rungs = HlsLadder.Select(height, _opts);

            // 3. Transcoder chaque échelon + uploader.
            var master = new StringBuilder("#EXTM3U\n#EXT-X-VERSION:3\n");
            foreach (var rung in rungs)
            {
                var rungDir = Path.Combine(work.FullName, rung.Name);
                var playlist = await _transcoder.TranscodeRungAsync(sourcePath, rungDir, rung.Height,
                    rung.VideoKbps, rung.AudioKbps, _opts.SegmentSeconds, ct);

                foreach (var f in Directory.EnumerateFiles(rungDir))
                {
                    var key = $"hls/{videoId}/{rung.Name}/{Path.GetFileName(f)}";
                    await using var fin = File.OpenRead(f);
                    var type = f.EndsWith(".m3u8") ? "application/vnd.apple.mpegurl" : "video/mp2t";
                    await _storage.PutAsync(_minio.HlsBucket, key, fin, fin.Length, type, ct);
                }

                var bandwidth = (rung.VideoKbps + rung.AudioKbps) * 1000;
                master.Append($"#EXT-X-STREAM-INF:BANDWIDTH={bandwidth},RESOLUTION=x{rung.Height}\n");
                master.Append($"{rung.Name}/{playlist}\n");

                _db.VideoRenditions.Add(new VideoRendition
                {
                    Id = Guid.NewGuid(), VideoId = videoId, Resolution = rung.Name,
                    Bitrate = rung.VideoKbps, ManifestKey = $"hls/{videoId}/{rung.Name}/{playlist}"
                });
            }

            // 4. Manifeste maître.
            var masterBytes = Encoding.UTF8.GetBytes(master.ToString());
            using (var ms = new MemoryStream(masterBytes))
                await _storage.PutAsync(_minio.HlsBucket, $"hls/{videoId}/master.m3u8", ms, masterBytes.Length,
                    "application/vnd.apple.mpegurl", ct);

            // 5. Finaliser.
            video.DurationSeconds = duration;
            video.Status = VideoStatus.Ready;
            video.UpdatedAt = DateTimeOffset.UtcNow;
            job.Status = TranscodeJobStatus.Succeeded;
            await _db.SaveChangesAsync(ct);

            await _storage.DeletePrefixAsync(_minio.OriginalsBucket, IUploadService.PartsPrefix(videoId), ct);
        }
        catch (Exception ex)
        {
            video.Status = VideoStatus.Failed;
            job.Status = TranscodeJobStatus.Failed;
            job.Error = ex.Message;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw; // laisse Hangfire gérer le retry
        }
        finally
        {
            try { work.Delete(recursive: true); } catch { /* best effort */ }
        }
    }
}
```

- [ ] **Step 5: Lancer → succès**

Run: `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter TranscodePipelineTests --nologo`
Expected: PASS (2 tests verts)

- [ ] **Step 6: Commit**

```bash
git add backend/src/MediaPlatform.Application/Interfaces/ITranscodePipeline.cs backend/src/MediaPlatform.Infrastructure/Media/TranscodePipeline.cs backend/tests/MediaPlatform.IntegrationTests/TranscodePipelineTests.cs
git commit -m "feat(media): pipeline de transcodage (orchestration) + tests"
```

---

## Task 7: Job Hangfire + endpoints upload + câblage Program.cs

**Files:**
- Create: `backend/src/MediaPlatform.Infrastructure/Media/TranscodeVideoJob.cs`
- Create: `backend/src/MediaPlatform.Api/Controllers/Dtos/CreateVideoRequest.cs`
- Modify: `backend/src/MediaPlatform.Api/Controllers/VideosController.cs`
- Modify: `backend/src/MediaPlatform.Api/Program.cs`

- [ ] **Step 1: Créer le job Hangfire**

`backend/src/MediaPlatform.Infrastructure/Media/TranscodeVideoJob.cs` :

```csharp
using Hangfire;
using MediaPlatform.Application.Interfaces;

namespace MediaPlatform.Infrastructure.Media;

/// <summary>Point d'entrée Hangfire : délègue au pipeline. Retries gérés par l'attribut.</summary>
public class TranscodeVideoJob
{
    private readonly ITranscodePipeline _pipeline;
    public TranscodeVideoJob(ITranscodePipeline pipeline) => _pipeline = pipeline;

    [AutomaticRetry(Attempts = 3)]
    public Task ExecuteAsync(Guid videoId) => _pipeline.RunAsync(videoId, CancellationToken.None);
}
```

- [ ] **Step 2: Créer le DTO de création**

`backend/src/MediaPlatform.Api/Controllers/Dtos/CreateVideoRequest.cs` :

```csharp
namespace MediaPlatform.Api.Controllers.Dtos;

public record CreateVideoRequest(string Title, string? Description, Guid? CategoryId);
```

- [ ] **Step 3: Étendre `VideosController` (endpoints upload)**

Remplacer le contenu de `VideosController.cs` par (les actions publiques existantes sont conservées) :

```csharp
using Hangfire;
using MediaPlatform.Api.Controllers.Dtos;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Media;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/videos")]
public class VideosController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IUploadService _upload;
    private readonly IBackgroundJobClient _jobs;

    public VideosController(AppDbContext db, IUploadService upload, IBackgroundJobClient jobs)
    {
        _db = db; _upload = upload; _jobs = jobs;
    }

    [HttpGet]
    public IActionResult List([FromQuery] string? q, [FromQuery] int page = 1)
        => Ok(new { items = Array.Empty<object>(), page, q });

    [HttpGet("{id:guid}")]
    public IActionResult Get(Guid id) => Ok(new { id });

    [HttpGet("{id:guid}/stream")]
    public IActionResult Stream(Guid id) => Ok(new { id, manifestUrl = (string?)null, renditions = Array.Empty<object>() });

    /// <summary>Initialise une vidéo (Draft). Réservé aux éditeurs.</summary>
    [HttpPost]
    [Authorize(Roles = "Editeur")]
    public async Task<IActionResult> Create([FromBody] CreateVideoRequest req, CancellationToken ct)
    {
        var ownerId = Guid.Parse(User.FindFirst("sub")!.Value);
        var video = new Video
        {
            Id = Guid.NewGuid(), Title = req.Title, Description = req.Description,
            CategoryId = req.CategoryId, OwnerId = ownerId, Status = VideoStatus.Draft,
            Slug = $"{Slugify(req.Title)}-{Guid.NewGuid():N}"[..Math.Min(320, req.Title.Length + 33)]
        };
        _db.Videos.Add(video);
        await _db.SaveChangesAsync(ct);
        return Ok(new { id = video.Id });
    }

    /// <summary>Téléverse un chunk (octets bruts dans le corps de la requête).</summary>
    [HttpPost("{id:guid}/upload/chunk")]
    [Authorize(Roles = "Editeur")]
    public async Task<IActionResult> UploadChunk(Guid id, [FromQuery] int index, CancellationToken ct)
    {
        if (!await _db.Videos.AnyAsync(v => v.Id == id && v.Status == VideoStatus.Draft, ct))
            return Problem(statusCode: 404, detail: "Vidéo introuvable ou déjà finalisée.");
        var received = await _upload.StoreChunkAsync(id, index, Request.Body, Request.ContentLength ?? 0, ct);
        return Ok(new { received });
    }

    /// <summary>Finalise l'upload et enfile le transcodage.</summary>
    [HttpPost("{id:guid}/upload/complete")]
    [Authorize(Roles = "Editeur")]
    public async Task<IActionResult> Complete(Guid id, [FromQuery] int total, CancellationToken ct)
    {
        var video = await _db.Videos.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (video is null) return Problem(statusCode: 404, detail: "Vidéo introuvable.");
        try
        {
            video.OriginalKey = await _upload.CompleteAsync(id, total, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: 409, detail: ex.Message);
        }
        video.Status = VideoStatus.Processing;
        await _db.SaveChangesAsync(ct);
        _jobs.Enqueue<TranscodeVideoJob>(j => j.ExecuteAsync(id));
        return Ok(new { id, status = video.Status.ToString() });
    }

    private static string Slugify(string s) =>
        new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-');
}
```

> Le claim `sub` provient du JWT de la Phase 1. Tant que l'auth n'est pas implémentée, ces endpoints renverront 401 (pas de token) — c'est attendu ; les tests d'intégration du pipeline (Task 6) ne passent pas par le contrôleur.

- [ ] **Step 4: Câbler `Program.cs`**

Remplacer `backend/src/MediaPlatform.Api/Program.cs` :

```csharp
// Point d'entrée. Mode API (défaut) ou worker Hangfire (--worker).
using Hangfire;
using Hangfire.PostgreSql;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Application.Media;
using MediaPlatform.Infrastructure.Media;
using MediaPlatform.Infrastructure.Persistence;
using MediaPlatform.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var isWorker = args.Contains("--worker");
var pg = builder.Configuration.GetConnectionString("Postgres")!;

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(pg));
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));
builder.Services.Configure<TranscodingOptions>(builder.Configuration.GetSection("Transcoding"));
builder.Services.AddSingleton<IObjectStorage, MinioObjectStorage>();
builder.Services.AddScoped<IUploadService, UploadService>();
builder.Services.AddScoped<IVideoTranscoder, FfmpegVideoTranscoder>();
builder.Services.AddScoped<ITranscodePipeline, TranscodePipeline>();
builder.Services.AddScoped<TranscodeVideoJob>();

builder.Services.AddHangfire(cfg => cfg.UsePostgreSqlStorage(o => o.UseNpgsqlConnection(pg)));
if (isWorker)
    builder.Services.AddHangfireServer();

if (!isWorker)
{
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

// Migrations + buckets au démarrage.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    sp.GetRequiredService<AppDbContext>().Database.Migrate();
    var storage = sp.GetRequiredService<IObjectStorage>();
    var minio = builder.Configuration.GetSection("Minio").Get<MinioOptions>() ?? new MinioOptions();
    storage.EnsureBucketAsync(minio.OriginalsBucket).GetAwaiter().GetResult();
    storage.EnsureBucketAsync(minio.HlsBucket).GetAwaiter().GetResult();
}

if (isWorker)
{
    app.Run();           // héberge seulement le serveur Hangfire
    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
```

> `Hangfire.PostgreSql` v1.20 : l'API de configuration est `UsePostgreSqlStorage(o => o.UseNpgsqlConnection(connStr))`. Si la version résolue expose la surcharge simple `UsePostgreSqlStorage(connStr)`, l'utiliser à la place (vérifier au build).

- [ ] **Step 5: Build**

Run: `dotnet build backend/MediaPlatform.sln --nologo`
Expected: PASS

- [ ] **Step 6: Lancer toute la suite de tests**

Run: `dotnet test backend/MediaPlatform.sln --nologo`
Expected: PASS (unitaires + intégration ; le test FFmpeg e2e de Task 8 est skippé si ffmpeg absent)

- [ ] **Step 7: Commit**

```bash
git add backend/src/MediaPlatform.Infrastructure/Media/TranscodeVideoJob.cs backend/src/MediaPlatform.Api
git commit -m "feat(media): endpoints upload + job Hangfire + cablage worker/API"
```

---

## Task 8: Test e2e FFmpeg (gated) + Dockerfile + compose + CLAUDE.md

**Files:**
- Test: `backend/tests/MediaPlatform.IntegrationTests/FfmpegSmokeTests.cs`
- Create: `backend/src/MediaPlatform.Api/Dockerfile`
- Modify: `docker-compose.yml`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Test e2e FFmpeg, skippé si ffmpeg absent**

`backend/tests/MediaPlatform.IntegrationTests/FfmpegSmokeTests.cs` :

```csharp
using System.Diagnostics;
using FluentAssertions;
using MediaPlatform.Infrastructure.Media;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class FfmpegSmokeTests
{
    private static bool FfmpegAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("ffmpeg", "-version")
                { RedirectStandardOutput = true, UseShellExecute = false });
            p!.WaitForExit();
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    [SkippableFact]
    public async Task Generates_hls_playlist_and_segments_from_a_tiny_clip()
    {
        Skip.IfNot(FfmpegAvailable(), "ffmpeg non installé sur ce runner");

        var work = Directory.CreateTempSubdirectory("ffmpeg-smoke-");
        var source = Path.Combine(work.FullName, "src.mp4");
        // Génère un clip 2s 640x480 de mire.
        using (var gen = Process.Start(new ProcessStartInfo("ffmpeg",
            $"-y -f lavfi -i testsrc=duration=2:size=640x480:rate=15 -pix_fmt yuv420p \"{source}\"")
            { UseShellExecute = false }))
        { gen!.WaitForExit(); }

        var t = new FfmpegVideoTranscoder();
        var (height, duration) = await t.ProbeAsync(source);
        height.Should().Be(480);
        duration.Should().BeApproximately(2, 0.5);

        var outDir = Path.Combine(work.FullName, "360p");
        await t.TranscodeRungAsync(source, outDir, 360, 800, 96, 6);

        File.Exists(Path.Combine(outDir, "index.m3u8")).Should().BeTrue();
        Directory.EnumerateFiles(outDir, "*.ts").Should().NotBeEmpty();

        work.Delete(recursive: true);
    }
}
```

Ajouter le package `Xunit.SkippableFact` au projet de tests d'intégration :

```xml
    <PackageReference Include="Xunit.SkippableFact" Version="1.4.*" />
```

- [ ] **Step 2: Lancer (skip ou pass selon ffmpeg local)**

Run: `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter FfmpegSmokeTests --nologo`
Expected: PASS ou SKIPPED (jamais FAIL)

- [ ] **Step 3: Créer le Dockerfile (API + worker, avec FFmpeg)**

`backend/src/MediaPlatform.Api/Dockerfile` :

```dockerfile
# Image multi-étapes : build .NET puis runtime avec FFmpeg (pour le worker).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/MediaPlatform.Api/MediaPlatform.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "MediaPlatform.Api.dll"]
```

> Le service `worker` de compose passe `--worker` en argument (déjà présent : `command: ["--worker"]`), capté par `Program.cs`.

- [ ] **Step 4: Ajouter les variables d'env MinIO/Postgres aux services api et worker**

Dans `docker-compose.yml`, ajouter sous `api` et `worker` un bloc `environment` (les services tournent dans le réseau compose, donc `Host=postgres` et `Endpoint=minio:9000` sont corrects) :

```yaml
    environment:
      ConnectionStrings__Postgres: "Host=postgres;Port=5432;Database=mediaplatform;Username=media;Password=media"
      Minio__Endpoint: "minio:9000"
      Minio__AccessKey: "minioadmin"
      Minio__SecretKey: "minioadmin"
      Minio__UseSsl: "false"
```

- [ ] **Step 5: Mettre à jour CLAUDE.md (note Hangfire/Redis → Postgres)**

Dans `CLAUDE.md`, section Stack, remplacer la ligne :

```
- **Jobs async** : Hangfire (file de transcodage) sur **Redis**.
```

par :

```
- **Jobs async** : Hangfire (file de transcodage), **storage PostgreSQL** (pas de storage
  Redis en OSS). Redis reste disponible pour du cache applicatif ultérieur.
```

- [ ] **Step 6: Build de l'image (vérifie le Dockerfile + ffmpeg)**

Run: `docker build -f backend/src/MediaPlatform.Api/Dockerfile -t map-api:test backend`
Expected: image construite ; `docker run --rm map-api:test --help` ne crashe pas au démarrage (échouera à se connecter à la DB hors compose, c'est attendu).

- [ ] **Step 7: Commit**

```bash
git add backend/tests/MediaPlatform.IntegrationTests/FfmpegSmokeTests.cs backend/src/MediaPlatform.Api/Dockerfile docker-compose.yml CLAUDE.md backend/tests/MediaPlatform.IntegrationTests/MediaPlatform.IntegrationTests.csproj
git commit -m "feat(media): smoke FFmpeg gated, Dockerfile+ffmpeg, compose env, note Hangfire/Postgres"
```

---

## Task 9: Vérification de bout en bout (manuelle, optionnelle)

- [ ] **Step 1: Lancer la pile et tester un upload réel**

```bash
docker compose up -d --build postgres redis minio api worker
```

Puis (une fois l'auth Phase 1 disponible, avec un token Éditeur) :

```bash
# Créer la vidéo
VID=$(curl -s -X POST http://localhost:8080/api/v1/videos -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" -d '{"title":"Test"}' | jq -r .id)
# Envoyer un fichier en un chunk
curl -s -X POST "http://localhost:8080/api/v1/videos/$VID/upload/chunk?index=0" \
  -H "Authorization: Bearer $TOKEN" --data-binary @sample.mp4
# Finaliser
curl -s -X POST "http://localhost:8080/api/v1/videos/$VID/upload/complete?total=1" \
  -H "Authorization: Bearer $TOKEN"
```

Expected : le worker transcode ; après quelques secondes, `hls/$VID/master.m3u8` existe dans MinIO (console http://localhost:9001) et la vidéo passe en `Ready`.

> Cette tâche est une validation manuelle ; elle dépend de l'auth (Phase 1). Si l'auth n'est pas encore là, retirer temporairement `[Authorize(Roles="Editeur")]` pour le test, puis le remettre.

---

## Self-Review (effectuée)

- **Couverture spec :** upload chunké (Task 4, 7) · stockage MinIO (Task 3) · ladder adaptative (Task 2) · transcodage FFmpeg (Task 5) · orchestration + statuts + retries + nettoyage (Task 6) · topologie worker/API (Task 7) · Dockerfile+ffmpeg+compose (Task 8) · note Hangfire/Postgres (Task 8). Tous les points de la spec ont une tâche.
- **À surveiller à l'exécution (incertitudes SDK/versions, signalées inline) :** identifiants par défaut de `MinioBuilder` (Task 3, Step 3) ; surcharge `UsePostgreSqlStorage` selon la version (Task 7, Step 4) ; nettoyer la classe `ThrowingTranscoder` du test (Task 6, Step 2) pour ne garder que les deux méthodes de l'interface.
- **Cohérence des types :** `IObjectStorage.PutAsync(... long size ...)` utilisé partout avec la taille ; `IUploadService.PartKey/PartsPrefix` réutilisés par le pipeline ; `IVideoTranscoder` (Probe + TranscodeRung) cohérent entre impl., fake et pipeline.
