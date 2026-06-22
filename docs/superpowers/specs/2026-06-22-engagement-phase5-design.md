# Phase 5 — Engagement — Design

> Commentaires, likes, partages (Phase 5 du [découpage](2026-06-21-plateforme-streaming-interne-design.md), §12).
> Dépend de l'auth Phase 1 (utilisateur authentifié) et de la visibilité Phase 3 (Published).
> Date : 2026-06-22.

## 1. Objectif et périmètre

Permettre aux visiteurs **authentifiés** de commenter, liker et partager les vidéos
**publiées**, et exposer les compteurs d'engagement (lecture anonyme).

**Dans le périmètre** : commentaires (créer / lister / supprimer le sien), likes
(liker / unliker, idempotent), partages (enregistrer), résumé d'engagement.

**Hors périmètre** : modération admin (masquer un commentaire) = Phase 6 ; réponses/threads ;
réactions multiples ; notifications ; télémétrie `/views`.

## 2. Décisions

- **Auth** : écriture réservée à tout utilisateur **authentifié** (`[Authorize]`, sans rôle
  spécifique), conformément à la spec. Lecture (liste commentaires, compteurs) anonyme.
- **Engagement uniquement sur vidéos visibles** (Published, ou propriétaire/Admin) — sinon 404
  (même règle de visibilité que la diffusion).
- **Like idempotent** : la contrainte d'unicité `(VideoId, UserId)` (déjà migrée) garantit un
  seul like ; re-like = no-op.
- **Commentaires** : pas de pré-modération (visible aussitôt, `IsModerated=false`) ; l'auteur
  (ou un Admin) peut supprimer le sien ; `GET` ne liste que les non modérés.
- **Aucune migration** : entités `Comment`/`Like`/`Share` et leurs index existent déjà.

## 3. API REST (`/api/v1/videos/{id}`)

| Méthode | Route | Auth | Comportement |
|---|---|---|---|
| `POST` | `/comments` | oui | body `CreateCommentRequest{ Body }` (trim, non vide, ≤ 4000) → crée. 201/200 + `CommentItem`. |
| `GET` | `/comments?page=` | non | `PagedResult<CommentItem>` non modérés, plus récents d'abord (page 20). |
| `DELETE` | `/comments/{commentId:guid}` | oui | auteur ou Admin → 204 ; non-auteur → 403 ; absent → 404. |
| `POST` | `/likes` | oui | like idempotent → `{ likeCount, likedByMe:true }`. |
| `DELETE` | `/likes` | oui | unlike → `{ likeCount, likedByMe:false }`. |
| `POST` | `/shares` | oui | body `ShareRequest{ Channel }` (non vide, ≤ 50) → `{ shareCount }`. |
| `GET` | `/engagement` | non* | `EngagementSummary{ LikeCount, CommentCount, ShareCount, LikedByMe }`. *`LikedByMe` selon le token s'il est présent. |

Toutes les écritures vérifient d'abord la **visibilité** de la vidéo (sinon 404).

## 4. Architecture logicielle

- **Application** : `IEngagementService` + DTOs (`CreateCommentRequest`, `ShareRequest`,
  `CommentItem(Guid Id, string Body, string AuthorDisplayName, DateTimeOffset CreatedAt)`,
  `EngagementSummary(int LikeCount, int CommentCount, int ShareCount, bool LikedByMe)`) ;
  réutilise `PagedResult<T>`. Exceptions : `VideoNotFoundException` (404, réutilisée),
  `CommentNotFoundException` (404), `NotCommentAuthorException` (403).
- **Infrastructure** : `EngagementService : IEngagementService` (`AppDbContext`). Le nom
  d'auteur est obtenu par jointure `Comments`×`Users` (l'entité `Comment` n'a que `UserId`).
- **Api** : `VideosController` — ajoute les actions d'engagement. `[Authorize]` sur les
  écritures ; lecture anonyme. Mappe les exceptions en `ProblemDetails`.

`IEngagementService` :
```csharp
Task<CommentItem> AddCommentAsync(Guid videoId, string body, Guid userId, CancellationToken ct);
Task<PagedResult<CommentItem>> ListCommentsAsync(Guid videoId, int page, CancellationToken ct);
Task DeleteCommentAsync(Guid videoId, Guid commentId, Guid userId, bool isAdmin, CancellationToken ct);
Task<EngagementSummary> LikeAsync(Guid videoId, Guid userId, CancellationToken ct);
Task<EngagementSummary> UnlikeAsync(Guid videoId, Guid userId, CancellationToken ct);
Task<int> ShareAsync(Guid videoId, string channel, Guid userId, CancellationToken ct);
Task<EngagementSummary> GetSummaryAsync(Guid videoId, Guid? userId, CancellationToken ct);
```

## 5. Règles & validations

- Corps de commentaire vide / blanc → `ArgumentException` → 400.
- `Channel` vide → 400.
- Like : si déjà liké, ne rien insérer (idempotent) ; renvoie le résumé.
- Visibilité : `Published`, ou `userId` propriétaire, ou `isAdmin` ; sinon `VideoNotFoundException`.
  (Pour la lecture anonyme du résumé/commentaires, seules les vidéos `Published` sont visibles.)

## 6. Gestion des erreurs

| Cas | Code |
|---|---|
| Vidéo non visible | 404 |
| Commentaire absent | 404 |
| Suppression par un non-auteur (non-Admin) | 403 |
| Corps/`channel` invalide | 400 |
| Écriture sans token | 401 |

## 7. Tests (TDD)

**EngagementService** (Postgres Testcontainer) :
- `AddComment` + `ListComments` (ordre récent→ancien, pagination, nom d'auteur) ;
- `DeleteComment` par l'auteur OK ; par un autre → `NotCommentAuthorException` ; Admin OK ;
- `Like` idempotent (double like → `LikeCount=1`) ; `Unlike` ; `LikedByMe` selon l'utilisateur ;
- `Share` → `ShareCount` incrémenté ;
- `GetSummary` agrège like/comment/share ;
- engagement sur vidéo non publiée → `VideoNotFoundException`.

**HTTP** (`WebApplicationFactory` + Postgres + MinIO) :
- `POST /comments` → 401 anonyme, 200 authentifié ; `GET /comments` liste ;
- `POST`/`DELETE /likes` → flux, `likeCount`/`likedByMe` cohérents ;
- `GET /engagement` → compteurs ;
- `DELETE /comments/{id}` par un non-auteur → 403.

## 8. Hors périmètre / YAGNI

Modération, threads, réactions, notifications, anti-spam/rate-limit, édition de commentaire,
partage générant un lien signé — phases ultérieures.
