# Phase 6 — Admin & analytique Implementation Plan

> **For agentic workers:** Implement task-by-task in TDD. Steps use checkbox (`- [ ]`).

**Goal:** Télémétrie de visionnage + statistiques, journal d'audit, extensions gestion utilisateurs, configuration système.

**Architecture:** 3 services Application/Infrastructure — `IAnalyticsService` (views+stats), `IAuditService` (write+list), `IAdminService` (users+config, appelle l'audit). Nouveau `AdminController` (stats/audit/config), `POST /views` dans `VideosController`, `UsersController` refactoré. Migration `AddSystemSettings` (seule entité nouvelle).

**Référence spec :** `docs/superpowers/specs/2026-06-22-admin-analytique-phase6-design.md`

---

## Task 1: Entité SystemSetting + migration

**Files:** `backend/src/MediaPlatform.Domain/Entities/SystemSetting.cs`, `AppDbContext.cs`, migration.

- [ ] **Step 1: Entité** `SystemSetting.cs` :
```csharp
namespace MediaPlatform.Domain.Entities;

/// <summary>Paramètre de configuration système (clé/valeur), modifiable par un Admin.</summary>
public class SystemSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 2: DbContext** — dans `AppDbContext`, ajouter le DbSet et la config. Ajouter la propriété :
```csharp
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
```
et dans `OnModelCreating`, avant `base.OnModelCreating(b);` :
```csharp
        b.Entity<SystemSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(200);
            e.Property(x => x.Value).IsRequired();
        });
```

- [ ] **Step 3: Migration** — `dotnet ef migrations add AddSystemSettings --project backend/src/MediaPlatform.Infrastructure --startup-project backend/src/MediaPlatform.Api`. Vérifier la création de la table `SystemSettings` (PK `Key`).

- [ ] **Step 4: Build** — `dotnet build backend/MediaPlatform.sln --nologo` → 0 erreurs.

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Domain/Entities/SystemSetting.cs backend/src/MediaPlatform.Infrastructure/Persistence
git commit -m "feat(admin): entite SystemSetting + migration"
```

---

## Task 2: DTOs, exceptions, interfaces (Application)

**Files:** `backend/src/MediaPlatform.Application/Admin/AdminDtos.cs`, `AdminExceptions.cs`, `backend/src/MediaPlatform.Application/Interfaces/IAnalyticsService.cs`, `IAuditService.cs`, `IAdminService.cs`.

- [ ] **Step 1: `AdminDtos.cs`**
```csharp
namespace MediaPlatform.Application.Admin;

public record RecordViewRequest(double WatchSeconds, string? SessionId);
public record TopVideo(Guid Id, string Title, int Views);
public record StatsSummary(int TotalVideos, IReadOnlyDictionary<string, int> VideosByStatus,
    int TotalUsers, long TotalViews, double TotalWatchSeconds, IReadOnlyList<TopVideo> TopVideos);
public record AuditEntry(Guid Id, Guid? ActorId, string Action, string EntityType, string? EntityId, DateTimeOffset OccurredAt);
public record UpdateUserRequest(bool IsActive, string? DisplayName);
public record UserAdminItem(Guid Id, string Email, string DisplayName, bool IsActive, IReadOnlyList<string> Roles);
public record SetConfigRequest(string Key, string Value);
```

- [ ] **Step 2: `AdminExceptions.cs`**
```csharp
namespace MediaPlatform.Application.Admin;

public class UserNotFoundException() : Exception("Utilisateur introuvable.");
public class UnknownRoleException(string role) : Exception($"Rôle inconnu : {role}.");
```

- [ ] **Step 3: Interfaces**

`IAnalyticsService.cs` :
```csharp
using MediaPlatform.Application.Admin;

namespace MediaPlatform.Application.Interfaces;

public interface IAnalyticsService
{
    Task RecordViewAsync(Guid videoId, double watchSeconds, string? sessionId, Guid? userId, CancellationToken ct = default);
    Task<StatsSummary> GetStatsAsync(CancellationToken ct = default);
}
```

`IAuditService.cs` :
```csharp
using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Catalog;

namespace MediaPlatform.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(Guid? actorId, string action, string entityType, string? entityId, string? metadata = null, CancellationToken ct = default);
    Task<PagedResult<AuditEntry>> ListAsync(int page, CancellationToken ct = default);
}
```

`IAdminService.cs` :
```csharp
using MediaPlatform.Application.Admin;

namespace MediaPlatform.Application.Interfaces;

public interface IAdminService
{
    Task<IReadOnlyList<UserAdminItem>> ListUsersAsync(CancellationToken ct = default);
    Task UpdateUserAsync(Guid id, bool isActive, string? displayName, Guid actorId, CancellationToken ct = default);
    Task AssignRoleAsync(Guid id, string role, Guid actorId, CancellationToken ct = default);
    Task RemoveRoleAsync(Guid id, string role, Guid actorId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetConfigAsync(CancellationToken ct = default);
    Task SetConfigAsync(string key, string value, Guid actorId, CancellationToken ct = default);
}
```

- [ ] **Step 4: Build** — `dotnet build backend/src/MediaPlatform.Application/MediaPlatform.Application.csproj --nologo` → 0 erreurs.

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Application/Admin backend/src/MediaPlatform.Application/Interfaces/IAnalyticsService.cs backend/src/MediaPlatform.Application/Interfaces/IAuditService.cs backend/src/MediaPlatform.Application/Interfaces/IAdminService.cs
git commit -m "feat(admin): DTOs, exceptions et interfaces (analytics/audit/admin)"
```

---

## Task 3: AuditService + tests

**Files:** `backend/src/MediaPlatform.Infrastructure/Admin/AuditService.cs`, `backend/tests/MediaPlatform.IntegrationTests/AuditServiceTests.cs`.

- [ ] **Step 1: Test** :
```csharp
using FluentAssertions;
using MediaPlatform.Infrastructure.Admin;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class AuditServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);

    [Fact]
    public async Task Log_then_list_newest_first()
    {
        var actor = Guid.NewGuid();
        await using (var db = NewDb()) await new AuditService(db).LogAsync(actor, "role.assign", "User", actor.ToString());
        await using (var db = NewDb()) await new AuditService(db).LogAsync(actor, "config.set", "Config", "theme");

        await using var read = NewDb();
        var page = await new AuditService(read).ListAsync(1);
        page.Items.Should().HaveCount(2);
        page.Items[0].Action.Should().Be("config.set"); // plus récent d'abord
    }
}
```

- [ ] **Step 2: Lancer → échec** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter AuditServiceTests --nologo`.

- [ ] **Step 3: Implémenter** `AuditService.cs` :
```csharp
using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Admin;

public class AuditService : IAuditService
{
    private const int PageSize = 20;
    private readonly AppDbContext _db;
    public AuditService(AppDbContext db) => _db = db;

    public async Task LogAsync(Guid? actorId, string action, string entityType, string? entityId, string? metadata = null, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), ActorId = actorId, Action = action,
            EntityType = entityType, EntityId = entityId, Metadata = metadata
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditEntry>> ListAsync(int page, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        var query = _db.AuditLogs.OrderByDescending(a => a.OccurredAt)
            .Select(a => new AuditEntry(a.Id, a.ActorId, a.Action, a.EntityType, a.EntityId, a.OccurredAt));
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);
        return new PagedResult<AuditEntry>(items, total, page, PageSize);
    }
}
```

- [ ] **Step 4: Lancer → succès** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter AuditServiceTests --nologo` (1 vert).

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Infrastructure/Admin/AuditService.cs backend/tests/MediaPlatform.IntegrationTests/AuditServiceTests.cs
git commit -m "feat(admin): AuditService (log + liste) + tests"
```

---

## Task 4: AnalyticsService + tests

**Files:** `backend/src/MediaPlatform.Infrastructure/Admin/AnalyticsService.cs`, `backend/tests/MediaPlatform.IntegrationTests/AnalyticsServiceTests.cs`.

- [ ] **Step 1: Test** :
```csharp
using FluentAssertions;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Admin;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class AnalyticsServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);
    private AnalyticsService Svc(AppDbContext db) => new(db);

    private async Task<Guid> SeedPublishedVideo()
    {
        var owner = Guid.NewGuid(); var vid = Guid.NewGuid();
        await using var db = NewDb();
        db.Users.Add(new User { Id = owner, Email = $"o{owner:N}@map.ma", DisplayName = "O", PasswordHash = "x" });
        db.Videos.Add(new Video { Id = vid, Title = "Vedette", Slug = $"s-{vid:N}", OwnerId = owner, Status = VideoStatus.Published });
        await db.SaveChangesAsync();
        return vid;
    }

    [Fact]
    public async Task RecordView_anonymous_then_stats_aggregate()
    {
        var vid = await SeedPublishedVideo();
        await using (var db = NewDb()) await Svc(db).RecordViewAsync(vid, 30.0, "sess-1", null);
        await using (var db = NewDb()) await Svc(db).RecordViewAsync(vid, 20.0, "sess-2", null);

        await using var read = NewDb();
        var stats = await Svc(read).GetStatsAsync();
        stats.TotalVideos.Should().Be(1);
        stats.VideosByStatus["Published"].Should().Be(1);
        stats.TotalViews.Should().Be(2);
        stats.TotalWatchSeconds.Should().Be(50.0);
        stats.TopVideos.Should().ContainSingle(t => t.Id == vid && t.Views == 2);
    }

    [Fact]
    public async Task RecordView_on_non_published_throws()
    {
        var owner = Guid.NewGuid(); var vid = Guid.NewGuid();
        await using (var db = NewDb())
        {
            db.Users.Add(new User { Id = owner, Email = $"o{owner:N}@map.ma", DisplayName = "O", PasswordHash = "x" });
            db.Videos.Add(new Video { Id = vid, Title = "Draft", Slug = $"s-{vid:N}", OwnerId = owner, Status = VideoStatus.Draft });
            await db.SaveChangesAsync();
        }
        await using var db2 = NewDb();
        var act = async () => await Svc(db2).RecordViewAsync(vid, 10, null, null);
        await act.Should().ThrowAsync<VideoNotFoundException>();
    }
}
```

- [ ] **Step 2: Lancer → échec**.

- [ ] **Step 3: Implémenter** `AnalyticsService.cs` :
```csharp
using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Catalog;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Domain.Enums;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Admin;

public class AnalyticsService : IAnalyticsService
{
    private readonly AppDbContext _db;
    public AnalyticsService(AppDbContext db) => _db = db;

    public async Task RecordViewAsync(Guid videoId, double watchSeconds, string? sessionId, Guid? userId, CancellationToken ct = default)
    {
        var published = await _db.Videos.AnyAsync(v => v.Id == videoId && v.Status == VideoStatus.Published, ct);
        if (!published) throw new VideoNotFoundException();

        _db.ViewEvents.Add(new ViewEvent
        {
            Id = Guid.NewGuid(), VideoId = videoId, UserId = userId,
            SessionId = sessionId ?? string.Empty,
            WatchSeconds = watchSeconds < 0 ? 0 : watchSeconds
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<StatsSummary> GetStatsAsync(CancellationToken ct = default)
    {
        var totalVideos = await _db.Videos.CountAsync(ct);
        var byStatus = await _db.Videos.GroupBy(v => v.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var totalUsers = await _db.Users.CountAsync(ct);
        var totalViews = await _db.ViewEvents.LongCountAsync(ct);
        var totalWatch = await _db.ViewEvents.SumAsync(v => (double?)v.WatchSeconds, ct) ?? 0;

        var top = await _db.ViewEvents.GroupBy(v => v.VideoId)
            .Select(g => new { VideoId = g.Key, Views = g.Count() })
            .OrderByDescending(x => x.Views).Take(5).ToListAsync(ct);
        var topIds = top.Select(t => t.VideoId).ToList();
        var titles = await _db.Videos.Where(v => topIds.Contains(v.Id))
            .Select(v => new { v.Id, v.Title }).ToListAsync(ct);
        var topVideos = top
            .Select(t => new TopVideo(t.VideoId, titles.FirstOrDefault(x => x.Id == t.VideoId)?.Title ?? "", t.Views))
            .ToList();

        return new StatsSummary(totalVideos,
            byStatus.ToDictionary(s => s.Key.ToString(), s => s.Count),
            totalUsers, totalViews, totalWatch, topVideos);
    }
}
```

- [ ] **Step 4: Lancer → succès** (2 verts).

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Infrastructure/Admin/AnalyticsService.cs backend/tests/MediaPlatform.IntegrationTests/AnalyticsServiceTests.cs
git commit -m "feat(admin): AnalyticsService (telemetrie + stats) + tests"
```

---

## Task 5: AdminService (users + config, avec audit) + tests

**Files:** `backend/src/MediaPlatform.Infrastructure/Admin/AdminService.cs`, `backend/tests/MediaPlatform.IntegrationTests/AdminServiceTests.cs`.

- [ ] **Step 1: Test** :
```csharp
using FluentAssertions;
using MediaPlatform.Application.Admin;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Admin;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MediaPlatform.IntegrationTests;

public class AdminServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    public async Task InitializeAsync() { await _pg.StartAsync(); await using var db = NewDb(); await db.Database.MigrateAsync(); }
    public Task DisposeAsync() => _pg.DisposeAsync().AsTask();
    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_pg.GetConnectionString()).Options);
    private AdminService Svc(AppDbContext db) => new(db, new AuditService(db));

    private async Task<Guid> NewUser()
    {
        var id = Guid.NewGuid();
        await using var db = NewDb();
        db.Users.Add(new User { Id = id, Email = $"u{id:N}@map.ma", DisplayName = "U", PasswordHash = "x" });
        await db.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task UpdateUser_deactivates_and_writes_audit()
    {
        var actor = await NewUser(); var target = await NewUser();
        await using (var db = NewDb()) await Svc(db).UpdateUserAsync(target, false, "Nouveau Nom", actor);
        await using var read = NewDb();
        var u = await read.Users.SingleAsync(x => x.Id == target);
        u.IsActive.Should().BeFalse(); u.DisplayName.Should().Be("Nouveau Nom");
        (await read.AuditLogs.AnyAsync(a => a.Action == "user.update" && a.EntityId == target.ToString())).Should().BeTrue();
    }

    [Fact]
    public async Task AssignRole_unknown_throws_and_RemoveRole_audited()
    {
        var actor = await NewUser(); var target = await NewUser();
        await using (var db = NewDb())
        {
            var act = async () => await Svc(db).AssignRoleAsync(target, "Inexistant", actor);
            await act.Should().ThrowAsync<UnknownRoleException>();
        }
        await using (var db = NewDb()) await Svc(db).AssignRoleAsync(target, "Editeur", actor);
        await using (var db = NewDb()) await Svc(db).RemoveRoleAsync(target, "Editeur", actor);
        await using var read = NewDb();
        (await read.UserRoles.CountAsync(ur => ur.UserId == target)).Should().Be(0);
        (await read.AuditLogs.CountAsync(a => a.Action == "role.remove")).Should().Be(1);
    }

    [Fact]
    public async Task Config_set_then_get_with_audit()
    {
        var actor = await NewUser();
        await using (var db = NewDb()) await Svc(db).SetConfigAsync("theme", "sombre", actor);
        await using (var db = NewDb()) await Svc(db).SetConfigAsync("theme", "clair", actor); // upsert
        await using var read = NewDb();
        var cfg = await Svc(read).GetConfigAsync();
        cfg["theme"].Should().Be("clair");
        (await read.AuditLogs.CountAsync(a => a.Action == "config.set")).Should().Be(2);
    }
}
```

- [ ] **Step 2: Lancer → échec**.

- [ ] **Step 3: Implémenter** `AdminService.cs` :
```csharp
using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Interfaces;
using MediaPlatform.Domain.Entities;
using MediaPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MediaPlatform.Infrastructure.Admin;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    public AdminService(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    public async Task<IReadOnlyList<UserAdminItem>> ListUsersAsync(CancellationToken ct = default) =>
        await _db.Users.Include(u => u.Roles).ThenInclude(ur => ur.Role)
            .Select(u => new UserAdminItem(u.Id, u.Email, u.DisplayName, u.IsActive,
                u.Roles.Select(ur => ur.Role!.Name).ToList()))
            .ToListAsync(ct);

    public async Task UpdateUserAsync(Guid id, bool isActive, string? displayName, Guid actorId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new UserNotFoundException();
        user.IsActive = isActive;
        if (!string.IsNullOrWhiteSpace(displayName)) user.DisplayName = displayName.Trim();
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "user.update", "User", id.ToString(), $"isActive={isActive}", ct);
    }

    public async Task AssignRoleAsync(Guid id, string role, Guid actorId, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new UserNotFoundException();
        var r = await _db.Roles.FirstOrDefaultAsync(x => x.Name == role, ct) ?? throw new UnknownRoleException(role);
        if (user.Roles.All(ur => ur.RoleId != r.Id))
        {
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = r.Id });
            await _db.SaveChangesAsync(ct);
        }
        await _audit.LogAsync(actorId, "role.assign", "User", id.ToString(), role, ct);
    }

    public async Task RemoveRoleAsync(Guid id, string role, Guid actorId, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new UserNotFoundException();
        var r = await _db.Roles.FirstOrDefaultAsync(x => x.Name == role, ct) ?? throw new UnknownRoleException(role);
        var link = user.Roles.FirstOrDefault(ur => ur.RoleId == r.Id);
        if (link is not null)
        {
            user.Roles.Remove(link);
            await _db.SaveChangesAsync(ct);
        }
        await _audit.LogAsync(actorId, "role.remove", "User", id.ToString(), role, ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetConfigAsync(CancellationToken ct = default) =>
        await _db.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    public async Task SetConfigAsync(string key, string value, Guid actorId, CancellationToken ct = default)
    {
        var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (setting is null) _db.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
        else { setting.Value = value; setting.UpdatedAt = DateTimeOffset.UtcNow; }
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(actorId, "config.set", "Config", key, value, ct);
    }
}
```

- [ ] **Step 4: Lancer → succès** (3 verts).

- [ ] **Step 5: Commit**
```
git add backend/src/MediaPlatform.Infrastructure/Admin/AdminService.cs backend/tests/MediaPlatform.IntegrationTests/AdminServiceTests.cs
git commit -m "feat(admin): AdminService (users + config, audite) + tests"
```

---

## Task 6: Controllers + DI + tests HTTP

**Files:** `Program.cs`, `VideosController.cs`, `UsersController.cs`, `backend/src/MediaPlatform.Api/Controllers/AdminController.cs`, `backend/tests/MediaPlatform.IntegrationTests/AdminEndpointsTests.cs`.

- [ ] **Step 1: DI** dans `Program.cs`, après `IEngagementService` :
```csharp
builder.Services.AddScoped<IAuditService, MediaPlatform.Infrastructure.Admin.AuditService>();
builder.Services.AddScoped<IAnalyticsService, MediaPlatform.Infrastructure.Admin.AnalyticsService>();
builder.Services.AddScoped<IAdminService, MediaPlatform.Infrastructure.Admin.AdminService>();
```

- [ ] **Step 2: `POST /views`** dans `VideosController` — injecter `IAnalyticsService _analytics` (ajouter au constructeur) + `using MediaPlatform.Application.Admin;`, et ajouter :
```csharp
    /// <summary>Télémétrie de visionnage (anonyme autorisé).</summary>
    [HttpPost("{id:guid}/views")]
    public async Task<IActionResult> RecordView(Guid id, [FromBody] RecordViewRequest req, CancellationToken ct)
    {
        try { await _analytics.RecordViewAsync(id, req.WatchSeconds, req.SessionId, CurrentUserIdOrNull(), ct); return NoContent(); }
        catch (VideoNotFoundException) { return Problem(statusCode: 404, detail: "Vidéo introuvable."); }
    }
```

- [ ] **Step 3: Refactor `UsersController`** sur `IAdminService` + ajout PUT et DELETE rôle :
```csharp
using MediaPlatform.Api.Controllers.Dtos;
using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly IAdminService _admin;
    public UsersController(IAdminService admin) => _admin = admin;

    private Guid ActorId() => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await _admin.ListUsersAsync(ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest req, CancellationToken ct)
        => await Run(() => _admin.UpdateUserAsync(id, req.IsActive, req.DisplayName, ActorId(), ct));

    [HttpPost("{id:guid}/roles")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleRequest req, CancellationToken ct)
        => await Run(() => _admin.AssignRoleAsync(id, req.Role, ActorId(), ct));

    [HttpDelete("{id:guid}/roles/{role}")]
    public async Task<IActionResult> RemoveRole(Guid id, string role, CancellationToken ct)
        => await Run(() => _admin.RemoveRoleAsync(id, role, ActorId(), ct));

    private async Task<IActionResult> Run(Func<Task> action)
    {
        try { await action(); return NoContent(); }
        catch (UserNotFoundException) { return Problem(statusCode: 404, detail: "Utilisateur introuvable."); }
        catch (UnknownRoleException ex) { return Problem(statusCode: 400, detail: ex.Message); }
    }
}
```
> `AssignRoleRequest` (record `Role`) existe déjà dans `Controllers/Dtos` (Phase 1).

- [ ] **Step 4: `AdminController.cs`** :
```csharp
using MediaPlatform.Application.Admin;
using MediaPlatform.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediaPlatform.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAnalyticsService _analytics;
    private readonly IAuditService _audit;
    private readonly IAdminService _admin;

    public AdminController(IAnalyticsService analytics, IAuditService audit, IAdminService admin)
    {
        _analytics = analytics; _audit = audit; _admin = admin;
    }

    private Guid ActorId() => Guid.Parse(User.FindFirst("sub")!.Value);

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct) => Ok(await _analytics.GetStatsAsync(ct));

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await _audit.ListAsync(page, ct));

    [HttpGet("config")]
    public async Task<IActionResult> GetConfig(CancellationToken ct) => Ok(await _admin.GetConfigAsync(ct));

    [HttpPut("config")]
    public async Task<IActionResult> SetConfig([FromBody] SetConfigRequest req, CancellationToken ct)
    {
        await _admin.SetConfigAsync(req.Key, req.Value, ActorId(), ct);
        return NoContent();
    }
}
```

- [ ] **Step 5: Build** — `dotnet build backend/MediaPlatform.sln --nologo` → 0 erreurs.

- [ ] **Step 6: Test HTTP** `AdminEndpointsTests.cs` :
```csharp
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
```

- [ ] **Step 7: Lancer** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --filter AdminEndpointsTests --nologo` → 4 verts.

- [ ] **Step 8: Suite d'intégration complète** — `dotnet test backend/tests/MediaPlatform.IntegrationTests --nologo` → tout vert (smoke skippé). En cas de flake de parallélisme, relancer.

- [ ] **Step 9: Commit**
```
git add backend/src/MediaPlatform.Api backend/tests/MediaPlatform.IntegrationTests/AdminEndpointsTests.cs
git commit -m "feat(admin): endpoints views/stats/audit/config + users PUT/DELETE role + DI + tests HTTP"
```

---

## Self-Review (effectuée)

- **Couverture spec :** SystemSetting + migration (Task 1) · DTOs/interfaces (Task 2) · audit (Task 3) · télémétrie+stats (Task 4) · users+config audités (Task 5) · endpoints + DI + HTTP (Task 6). Tous les points ont une tâche.
- **Cohérence des types :** `IAnalyticsService`/`IAuditService`/`IAdminService` identiques interface/impl/contrôleur ; `PagedResult<T>` réutilisé ; claims `sub`/`role` cohérents (`CurrentUserIdOrNull`/`ActorId`/`IsAdmin`).
- **Incertitudes :** disponibilité `dotnet ef` (déjà installé en Phase 3) ; le test HTTP suppose le seed admin actif (`Seed:AdminEmail/Password` fournis à la factory — contrairement aux autres tests qui le désactivent).
- **Sécurité :** `/stats`/`/audit`/`/config`/`/users` réservés Admin ; `/views` anonyme ; désactivation (pas de suppression) ; audit des actions sensibles.
```
