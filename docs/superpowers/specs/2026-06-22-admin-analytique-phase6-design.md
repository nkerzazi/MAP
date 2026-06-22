# Phase 6 — Admin & analytique — Design

> Gestion utilisateurs/rôles, télémétrie & statistiques, journal d'audit, configuration système
> (Phase 6 du [découpage](2026-06-21-plateforme-streaming-interne-design.md), §12). **Dernière phase.**
> Date : 2026-06-22.

## 1. Objectif et périmètre

Compléter la plateforme avec les fonctions d'administration et d'analytique :
télémétrie de visionnage, statistiques globales, journal d'audit des actions sensibles,
extensions de gestion des utilisateurs, et configuration système modifiable.

**Dans le périmètre** : `POST /views`, `GET /stats`, `GET /audit` + écriture d'audit,
`PUT /users/{id}` (dés)activation, `DELETE /users/{id}/roles/{role}`, `GET/PUT /config`.

**Hors périmètre** : séries temporelles, export, purge/rétention des `ViewEvent`,
permissions fines, 2FA, rate-limiting.

## 2. Décisions

- **Désactivation, pas suppression** : un utilisateur est désactivé (`IsActive=false`),
  jamais supprimé physiquement (préserve l'intégrité référentielle).
- **Audit explicite** : un `AuditService` est appelé dans les actions admin sensibles
  (rôle attribué/retiré, utilisateur (dés)activé, config modifiée). Pas d'intercepteur global.
- **Stats résumé + top N** : agrégations SQL à la volée (pas de séries temporelles).
- **Télémétrie anonyme** : `POST /views` accepte les visiteurs anonymes (UserId null).
- **Migration** : seule l'entité nouvelle `SystemSetting` ajoute une migration ; `ViewEvent`
  et `AuditLog` sont déjà mappés.

## 3. Modèle de données

Entité nouvelle (migration `AddSystemSettings`) :
```csharp
public class SystemSetting
{
    public string Key { get; set; } = string.Empty;   // clé primaire
    public string Value { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```
Config EF : `HasKey(x => x.Key)` ; `Value` requis. Ajout du `DbSet<SystemSetting> SystemSettings`.
`ViewEvent` (VideoId, UserId?, SessionId, WatchSeconds, OccurredAt) et `AuditLog`
(ActorId?, Action, EntityType, EntityId?, Metadata?, OccurredAt) existent déjà.

## 4. API REST

| Méthode | Route | Auth | Comportement |
|---|---|---|---|
| `POST` | `/api/v1/videos/{id}/views` | non | body `{ watchSeconds, sessionId? }` → enregistre un `ViewEvent` (vidéo Published, sinon 404). 204. |
| `GET` | `/api/v1/stats` | Admin | `StatsSummary`. |
| `GET` | `/api/v1/audit?page=` | Admin | `PagedResult<AuditEntry>`, plus récentes d'abord. |
| `GET` | `/api/v1/users` | Admin | liste (existant). |
| `POST` | `/api/v1/users/{id}/roles` | Admin | attribue un rôle (existant) — **audité**. |
| `DELETE` | `/api/v1/users/{id}/roles/{role}` | Admin | retire un rôle — **audité**. 404 si user inconnu, 400 si rôle inconnu. |
| `PUT` | `/api/v1/users/{id}` | Admin | body `{ isActive, displayName? }` → (dés)activation + renommage — **audité**. |
| `GET` | `/api/v1/config` | Admin | toutes les clés/valeurs. |
| `PUT` | `/api/v1/config` | Admin | body `{ key, value }` → upsert — **audité**. |

`StatsSummary` :
```csharp
public record StatsSummary(
    int TotalVideos, IReadOnlyDictionary<string,int> VideosByStatus,
    int TotalUsers, long TotalViews, double TotalWatchSeconds,
    IReadOnlyList<TopVideo> TopVideos);
public record TopVideo(Guid Id, string Title, int Views);
```

## 5. Architecture logicielle

Trois services (cohérents avec Catalog/Streaming/Engagement) :

- **Application** :
  - `IAnalyticsService` : `Task RecordViewAsync(Guid videoId, double watchSeconds, string? sessionId, Guid? userId, ct)` ; `Task<StatsSummary> GetStatsAsync(ct)`.
  - `IAuditService` : `Task LogAsync(Guid? actorId, string action, string entityType, string? entityId, string? metadata = null, ct)` ; `Task<PagedResult<AuditEntry>> ListAsync(int page, ct)`.
  - `IAdminService` : `Task<IReadOnlyList<UserAdminItem>> ListUsersAsync(ct)` ; `Task UpdateUserAsync(Guid id, bool isActive, string? displayName, Guid actorId, ct)` ; `Task AssignRoleAsync(Guid id, string role, Guid actorId, ct)` ; `Task RemoveRoleAsync(Guid id, string role, Guid actorId, ct)` ; `Task<IReadOnlyDictionary<string,string>> GetConfigAsync(ct)` ; `Task SetConfigAsync(string key, string value, Guid actorId, ct)`.
  - DTOs : `RecordViewRequest(double WatchSeconds, string? SessionId)`, `StatsSummary`/`TopVideo`, `AuditEntry(Guid Id, Guid? ActorId, string Action, string EntityType, string? EntityId, DateTimeOffset OccurredAt)`, `UpdateUserRequest(bool IsActive, string? DisplayName)`, `UserAdminItem(Guid Id, string Email, string DisplayName, bool IsActive, IReadOnlyList<string> Roles)`, `SetConfigRequest(string Key, string Value)`. Exceptions : `UserNotFoundException` (404), `UnknownRoleException` (400).
- **Infrastructure** : `AnalyticsService`, `AuditService`, `AdminService` (chacun `AppDbContext`). `AdminService` dépend de `IAuditService`. La visibilité de `RecordView` réutilise la règle Published.
- **Api** :
  - `VideosController` : ajout `POST /{id}/views` (anonyme) via `IAnalyticsService`.
  - `UsersController` : refactoré sur `IAdminService` ; ajout `PUT /{id}` et `DELETE /{id}/roles/{role}` ; extrait l'identité (`sub`) comme acteur d'audit.
  - Nouveau `AdminController` (`/api/v1/...`, `[Authorize(Roles="Admin")]`) : `GET /stats`, `GET /audit`, `GET/PUT /config`.

## 6. Gestion des erreurs

| Cas | Code |
|---|---|
| `POST /views` sur vidéo non publiée | 404 |
| Action admin sans token / non-Admin | 401 / 403 |
| Utilisateur inconnu | 404 |
| Rôle inconnu | 400 |

## 7. Tests (TDD)

**AnalyticsService** (Postgres Testcontainer) :
- `RecordView` anonyme (UserId null) et authentifié → `ViewEvent` persisté ; sur non-publiée → `VideoNotFoundException` ;
- `GetStats` : `VideosByStatus`, `TotalVideos/Users/Views`, `TotalWatchSeconds`, `TopVideos` triés.

**AuditService** : `Log` puis `List` (paginé, ordre récent→ancien).

**AdminService** : `UpdateUser` désactive + écrit un audit ; `RemoveRole` retire + audit ; `AssignRole` rôle inconnu → `UnknownRoleException` ; `GetConfig`/`SetConfig` upsert + audit.

**HTTP** (`WebApplicationFactory` + Postgres + MinIO) :
- `POST /views` anonyme → 204 ;
- `GET /stats` : 401 anonyme, 403 visiteur, 200 admin (avec compteurs) ;
- `PUT /users/{id}` (admin) désactive ; `GET /audit` contient l'entrée ;
- `GET/PUT /config` admin.

## 8. Hors périmètre / YAGNI

Séries temporelles, dashboards, export CSV, purge ViewEvent, permissions granulaires,
versionnement de config, notifications admin — au-delà du PFE.
