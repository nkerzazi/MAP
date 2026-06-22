# Phase 3 — Catalogue éditeur — Design

> Conception du catalogue éditeur (Phase 3 du [découpage de la spec principale](2026-06-21-plateforme-streaming-interne-design.md), §12).
> Dépend de l'auth Phase 1 (rôles `Editeur`/`Admin`, claim `sub`) et du pipeline Phase 2 (statut `Ready`).
> Date : 2026-06-22.

## 1. Objectif et périmètre

Rendre le catalogue réel : édition des métadonnées d'une vidéo, cycle de publication
(`publish`/`archive`), listing des vidéos de l'éditeur, et **catalogue public** avec
**pagination + recherche full-text PostgreSQL**.

**Dans le périmètre** : `PUT /videos/{id}`, `POST /videos/{id}/publish`,
`POST /videos/{id}/archive`, `GET /videos/mine`, `GET /videos` (réel), `GET /videos/{id}` (réel),
recherche `tsvector`, autorisation propriétaire/Admin.

**Hors périmètre** : diffusion HLS réelle (`/stream` reste Phase 4), commentaires/likes/partages
(Phase 5), modération, CRUD des catégories (supposées exister — seed/gestion ultérieure).

## 2. Décisions d'architecture

- **Recherche full-text** : colonne `Video.SearchVector` (`NpgsqlTsVector`) **générée par
  PostgreSQL** sur `Title`+`Description` (configuration `french`), **index GIN**, via
  `HasGeneratedTsVectorColumn` (Npgsql EF Core). Maintenue par la DB (pas de trigger
  applicatif, jamais désynchronisée). Le filtre par **tag** se fait par jointure séparée ;
  les noms de tags ne sont **pas** dans le `tsvector` (la requête `q` couvre titre+description).
- **Propriété** : un `Editeur` ne modifie/publie/archive **que ses propres vidéos**
  (`OwnerId == utilisateur courant`) ; un `Admin` peut agir sur toutes. Violation → **403**.
- **Transitions** : `publish` exige `Ready` (ou `Archived` pour re-publication) → `Published`
  (+ `PublishedAt`) ; `archive` : `Published` → `Archived`. État invalide → **409**.
- **Slug stable** : la mise à jour du titre **ne change pas** le slug (permalink).

## 3. Modèle de données (migration `AddVideoSearchVector`)

Ajout à l'entité `Video` :
```csharp
public NpgsqlTsVector SearchVector { get; set; } = null!;
```
Configuration EF (`OnModelCreating`, entité `Video`) :
```csharp
e.HasGeneratedTsVectorColumn(x => x.SearchVector, "french", x => new { x.Title, x.Description })
 .HasIndex(x => x.SearchVector).HasMethod("GIN");
```
> `Description` est nullable : Npgsql gère le `coalesce` dans l'expression générée.
Migration EF Core dédiée ; aucune autre table modifiée.

## 4. API REST

### 4.1 Éditeur (propriétaire seul, ou Admin)

`[Authorize(Roles = "Editeur,Admin")]` au niveau des actions ; la **propriété** est vérifiée
dans le service (sauf Admin).

| Méthode | Route | Comportement |
|---|---|---|
| `PUT` | `/api/v1/videos/{id}` | Body `UpdateVideoRequest` (Title, Description?, CategoryId?, Tags[]). Met à jour les métadonnées ; résout/crée les `Tag` par nom et réécrit les `VideoTag` ; `CategoryId` inexistant → 400. Slug inchangé. Non-propriétaire (non-Admin) → 403 ; vidéo absente → 404. |
| `POST` | `/api/v1/videos/{id}/publish` | `Ready`/`Archived` → `Published`, fixe `PublishedAt`. Autre état → 409. |
| `POST` | `/api/v1/videos/{id}/archive` | `Published` → `Archived`. Autre état → 409. |
| `GET` | `/api/v1/videos/mine?page=` | Vidéos dont `OwnerId == courant` (tous statuts), paginé, triées par `UpdatedAt` desc. |

### 4.2 Public (anonyme)

| Méthode | Route | Comportement |
|---|---|---|
| `GET` | `/api/v1/videos?q=&page=&categoryId=&tag=` | Uniquement `Published`. Recherche FT sur `q` (`SearchVector.Matches(websearch_to_tsquery('french', q))`) si fourni. Filtres optionnels `categoryId` et `tag` (par nom, via jointure). Tri : pertinence si `q`, sinon `PublishedAt` desc. Pagination taille **20**. Renvoie `PagedResult { items, total, page, pageSize }`. |
| `GET` | `/api/v1/videos/{id}` | `Published` → visible par tous (renvoie `VideoDetail` : métadonnées, catégorie, tags, renditions). Non publiée → visible **uniquement** par le propriétaire/Admin authentifié, sinon **404** (pas de fuite d'existence). |

> `GET /videos/{id}/stream` reste le stub Phase 2 (implémenté en Phase 4).

## 5. Architecture logicielle

- **Application** : `ICatalogService` + DTOs (`UpdateVideoRequest`, `VideoListItem`,
  `VideoDetail`, `PagedResult<T>`), exceptions métier (`VideoNotFoundException` → 404,
  `NotVideoOwnerException` → 403, `InvalidVideoStateException` → 409).
- **Infrastructure** : `CatalogService : ICatalogService` (dépend de `AppDbContext`).
  Reçoit l'identité courante (`Guid userId`, `bool isAdmin`) en paramètre des opérations
  protégées ; applique la règle propriétaire/Admin et les transitions.
- **Api** : `VideosController` mince — extrait `userId` du claim `sub` et `isAdmin` du claim
  `role`, délègue au service, mappe les exceptions en `ProblemDetails`. Les actions éditeur
  portent `[Authorize(Roles="Editeur,Admin")]` ; les actions publiques restent anonymes.

`CatalogService` est un nouveau fichier focalisé ; il ne touche pas au pipeline (Phase 2).

## 6. Gestion des erreurs

| Cas | Code |
|---|---|
| Vidéo inexistante (ou non visible par un anonyme) | 404 |
| Action sur la vidéo d'autrui (non-Admin) | 403 |
| Transition d'état invalide (ex. publier une `Draft`) | 409 |
| `CategoryId` inexistant à l'update | 400 |
| Action éditeur sans token / mauvais rôle | 401 / 403 |

Toutes via `ProblemDetails`.

## 7. Tests (TDD)

**Intégration `CatalogService`** (Postgres Testcontainer) :
- update par le propriétaire OK ; par un autre éditeur → `NotVideoOwnerException` ; par Admin OK ;
- `CategoryId` inconnu → erreur 400 ; tags créés/résolus et `VideoTag` réécrits ;
- `publish` : `Ready`→`Published` (+ `PublishedAt`), `Draft`→`InvalidVideoStateException`,
  `archive` `Published`→`Archived`, re-publish `Archived`→`Published` ;
- `mine` renvoie toutes les vidéos du propriétaire, paginées.

**HTTP** (`WebApplicationFactory` + Postgres + MinIO) :
- `GET /videos` ne renvoie que `Published`, paginé ; `q` matche un mot du titre/description ;
  filtre `categoryId`/`tag` ;
- `GET /videos/{id}` : `Published` visible anonyme ; `Draft` → 404 pour un anonyme/non-propriétaire ;
- `PUT`/`publish`/`archive` : 401 sans token, 403 mauvais rôle ou non-propriétaire, 200 propriétaire/Admin.

**Unitaire** : sélection/normalisation des tags (déduplication, trim) si extraite en fonction pure.

## 8. Hors périmètre / YAGNI

CRUD catégories, recherche par tags dans le `tsvector`, tri par popularité, facettes,
pagination par curseur, soft-delete, historique de versions — phases ultérieures.
