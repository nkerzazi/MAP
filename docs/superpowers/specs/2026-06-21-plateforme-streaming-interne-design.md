# MAP — Plateforme de gestion et de diffusion de contenus vidéo

> **Nom du projet : MAP**
> Projet de Fin d'Études — Youssra Zounaki — MAP (Maghreb Arabe Presse) / EHEI Oujda
> Document de conception (spec) — 2026-06-21

## 1. Contexte et objectif

La MAP souhaite une **plateforme web interne, entièrement auto-hébergée**, pour le
téléversement, le traitement, le stockage et la diffusion de contenus vidéo sur son
site officiel, **sans dépendance à des services externes** (ni YouTube, ni Vimeo, ni
CDN tiers, ni stockage cloud public). Tous les composants (transcodage, stockage objet,
base de données, lecteur vidéo) s'exécutent sur l'infrastructure de la MAP.

Objectifs :
- Autonomie technologique et contrôle total des données et de la confidentialité.
- Pipeline de traitement vidéo automatisé (transcodage + streaming adaptatif HLS).
- Trois interfaces : **Administration**, **Éditeur**, **Utilisateur (visiteur)**.
- Lecteur vidéo développé en interne, **accessible via API** (intégrable ailleurs sur
  le site de la MAP).

## 2. Stack technique retenue

| Couche | Technologie | Rôle |
|---|---|---|
| Backend | **ASP.NET Core 8 Web API** (C# 12) | API REST, logique métier |
| ORM | **Entity Framework Core 8** | Accès données, migrations |
| Base de données | **PostgreSQL 16** | Persistance (utilisateurs, vidéos, métadonnées, engagement, stats, audit) |
| Jobs asynchrones | **Hangfire** (+ Redis) | File de transcodage, retries, notifications |
| Cache / file | **Redis 7** | Cache + backend de file Hangfire |
| Stockage objet | **MinIO** (S3-compatible, auto-hébergé) | Vidéos originales + segments/manifestes HLS |
| Traitement vidéo | **FFmpeg 6** | Transcodage multi-débit → HLS |
| Streaming | **HLS** (HTTP Live Streaming) | Diffusion adaptative |
| Frontend | **Angular 17+** | SPA, 3 modules par rôle |
| Lecteur | **hls.js** | Lecteur vidéo interne, responsive |
| Reverse proxy | **Nginx** | Front API + diffusion des segments HLS |
| Auth | **ASP.NET Core Identity + JWT** | Authentification, RBAC |
| Orchestration | **Docker Compose** | Déploiement auto-hébergé tout-en-un |

**Note pour le mémoire (correspondance MVC) :** l'architecture reste conforme au modèle
MVC décrit dans le rapport — la *Vue* est l'app Angular, le *Contrôleur* correspond aux
controllers + services applicatifs, le *Modèle* aux entités EF Core. Le backend adopte
en plus un découpage en couches (Domain / Application / Infrastructure / Api) pour la
maintenabilité.

Divergences assumées par rapport au rapport V3 (à mettre à jour dans le mémoire) :
- **PostgreSQL** au lieu de Microsoft SQL Server (open-source, sans licence, cohérent
  avec une plateforme 100 % auto-hébergée et open-source).
- **MinIO** comme stockage objet au lieu du système de fichiers brut.

## 3. Architecture globale

```
[Angular 17 SPA]  ──REST + JWT──►  [ASP.NET Core 8 Web API]  ──►  [PostgreSQL 16]
  (hls.js player)                       │   │   │
                                        │   │   └─►  [Redis]   (cache + file Hangfire)
                                        │   └─────►  [MinIO]   (originaux + HLS)
                                        └─────────►  [Hangfire Worker] ─► [FFmpeg] (→ HLS)
                       [Nginx reverse proxy] : front API + diffusion segments HLS
```

L'application Angular est **unique**, avec trois modules protégés par garde de rôle :
`admin`, `editor`, `viewer`. Le lecteur consomme `GET /api/v1/videos/{id}/stream`
(retourne l'URL du manifeste HLS + métadonnées), ce qui rend le contenu **intégrable
via API** ailleurs sur le site de la MAP.

## 4. Modules et responsabilités

| Module | Responsabilité | Acteur principal |
|---|---|---|
| Identity & RBAC | Auth JWT, rôles Admin/Éditeur/Visiteur | Tous |
| Gestion utilisateurs | CRUD utilisateurs + affectation de rôles | Admin |
| Catalogue & métadonnées | CRUD vidéos, catégories/tags, publication/archivage, recherche & indexation | Éditeur |
| Pipeline média | Upload chunké → MinIO → file → FFmpeg HLS → état « Ready » | Éditeur (système) |
| Diffusion/streaming | Manifeste + segments HLS (URLs présignées via Nginx) | Visiteur |
| Engagement | Commentaires, likes, partages | Visiteur (authentifié) |
| Analytique | Événements de vue, temps de visionnage, contenus les plus consultés | Admin/Éditeur |
| Audit/journalisation | Journalisation des actions sensibles | Admin |

## 5. Modèle de données (PostgreSQL / EF Core)

Entités principales :

- **User** (Id, Email, PasswordHash, DisplayName, IsActive, CreatedAt)
- **Role** (Admin, Editeur, Visiteur) + **UserRole**
- **Video** (Id, Title, Description, Slug, Status, CategoryId, OwnerId, OriginalKey,
  Duration, PublishedAt, CreatedAt, UpdatedAt, SearchVector)
- **Category** (Id, Name, Slug) · **Tag** (Id, Name) · **VideoTag**
- **VideoRendition** (Id, VideoId, Resolution [360p/720p/1080p], Bitrate, ManifestKey)
- **TranscodeJob** (Id, VideoId, Status, Progress, Error, Attempts, CreatedAt)
- **Comment** (Id, VideoId, UserId, Body, CreatedAt, IsModerated)
- **Like** (Id, VideoId, UserId, CreatedAt) — unique (VideoId, UserId)
- **Share** (Id, VideoId, UserId?, Channel, CreatedAt)
- **ViewEvent** (Id, VideoId, UserId?, WatchSeconds, OccurredAt, SessionId)
- **AuditLog** (Id, ActorId, Action, EntityType, EntityId, Metadata, OccurredAt)

**Cycle de vie d'une vidéo :**
`Draft → Processing → Ready → Published → Archived` (+ `Failed` en cas d'échec du pipeline).

**Recherche & indexation :** PostgreSQL full-text (`tsvector` sur titre/description/tags),
mise à jour via trigger ou à l'enregistrement.

## 6. API REST (versionnée `/api/v1`)

Authentification JWT (Bearer). Réponses d'erreur normalisées via `ProblemDetails`.

- **Public / Visiteur**
  - `GET /videos` (catalogue publié, pagination, recherche `?q=`)
  - `GET /videos/{id}` (détail + métadonnées)
  - `GET /videos/{id}/stream` (URL manifeste HLS + variantes)
  - `POST /videos/{id}/comments` · `POST /videos/{id}/likes` · `POST /videos/{id}/shares` *(auth requise)*
  - `POST /videos/{id}/views` (télémétrie de visionnage)
  - `POST /auth/login` · `POST /auth/register` *(selon politique MAP)*
- **Éditeur** *(rôle Editeur)*
  - `POST /videos` (init upload) · `POST /videos/{id}/upload` (chunks)
  - `PUT /videos/{id}` (métadonnées) · `POST /videos/{id}/publish` · `POST /videos/{id}/archive`
  - `GET /videos/mine` · recherche/indexation
- **Admin** *(rôle Admin)*
  - `GET/POST/PUT/DELETE /users` · `POST /users/{id}/roles`
  - `GET /stats` (vues, temps de visionnage, top contenus) · `GET /audit`
  - `GET/PUT /config` (configuration système)

**Règle d'accès confirmée :** visionnage **anonyme** autorisé ; authentification
**requise** pour commenter, liker et partager.

## 7. Pipeline vidéo (détail)

1. L'éditeur initialise une vidéo (`Draft`) et téléverse le fichier en **chunks**.
2. Le fichier original est stocké dans MinIO (`originals/{videoId}/source.ext`).
3. Un job Hangfire est mis en file ; la vidéo passe en `Processing`.
4. FFmpeg génère un **HLS multi-débit** (360p/720p/1080p) + manifeste maître ;
   segments et `.m3u8` stockés dans MinIO (`hls/{videoId}/...`).
5. Succès → `Ready` (publiable par l'éditeur) ; échec → `Failed` + retry Hangfire +
   notification éditeur.
6. Publication → `Published` (apparaît au catalogue public).

## 8. Gestion des erreurs

- Middleware global d'exceptions → `ProblemDetails`.
- Validation d'upload : format/MIME/taille autorisés.
- Échecs de transcodage : job `Failed`, retries Hangfire (n tentatives) puis notification.
- Expiration des URLs présignées : rafraîchissement côté lecteur.

## 9. Tests

- **Backend :** xUnit (unitaires) + intégration avec **Testcontainers** (PostgreSQL +
  MinIO réels en CI).
- **Frontend :** Jest (unitaires) + Cypress (e2e) sur les trois interfaces.
- **Pipeline :** transcoder un clip d'exemple, vérifier la génération du manifeste HLS
  et des segments.

## 10. Arborescence du projet

```
MAP/
├─ CLAUDE.md
├─ docker-compose.yml                  # postgres, redis, minio, nginx, api, worker
├─ docs/superpowers/specs/2026-06-21-…-design.md
├─ backend/
│  ├─ MediaPlatform.sln
│  ├─ src/
│  │  ├─ MediaPlatform.Domain/         # entités, enums, value objects
│  │  ├─ MediaPlatform.Application/    # services, DTOs, interfaces
│  │  ├─ MediaPlatform.Infrastructure/ # EF Core, MinIO, FFmpeg, Hangfire
│  │  └─ MediaPlatform.Api/            # controllers, auth, middleware
│  └─ tests/
│     ├─ MediaPlatform.UnitTests/
│     └─ MediaPlatform.IntegrationTests/
├─ frontend/                           # workspace Angular 17
│  └─ src/app/{core,admin,editor,viewer,shared}
└─ infra/nginx/
```

## 11. Déploiement

`docker-compose up` lance l'ensemble (PostgreSQL, Redis, MinIO, API, worker Hangfire,
Nginx) sur un serveur de la MAP — déploiement auto-hébergé tout-en-un, sans dépendance
externe.

## 12. Découpage en phases (pour le plan d'implémentation)

1. **Socle** : scaffold, docker-compose, EF Core + migrations, Identity + JWT, RBAC.
2. **Pipeline média** : upload chunké, MinIO, Hangfire + FFmpeg → HLS.
3. **Catalogue éditeur** : CRUD vidéos, métadonnées, publication/archivage, recherche.
4. **Diffusion & viewer** : streaming HLS, lecteur hls.js, API de stream.
5. **Engagement** : commentaires, likes, partages.
6. **Admin & analytique** : gestion utilisateurs/rôles, stats, audit, config.
