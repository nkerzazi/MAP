# Architecture — Plateforme de diffusion vidéo MAP

> Document d'architecture technique de la plateforme interne de gestion et de diffusion
> de contenus vidéo de la **MAP (Maghreb Arabe Presse)**. Solution **entièrement
> auto-hébergée** : téléversement, transcodage, stockage objet et diffusion **HLS**, sans
> aucun service externe (pas de YouTube/Vimeo, pas de cloud public, pas de CDN tiers).

**Sommaire**

1. [Vue d'ensemble](#1-vue-densemble)
2. [Composants applicatifs](#2-composants-applicatifs)
3. [Modèle de données](#3-modèle-de-données)
4. [Téléversement (upload chunké)](#4-téléversement-upload-chunké)
5. [Encodeur vidéo & pipeline de transcodage](#5-encodeur-vidéo--pipeline-de-transcodage)
6. [Streaming HLS](#6-streaming-hls)
7. [Cycle de vie d'une vidéo (transversal)](#7-cycle-de-vie-dune-vidéo-transversal)
8. [Commentaires & likes (engagement)](#8-commentaires--likes-engagement)
9. [Accès : Admin, Éditeur, Visiteur](#9-accès--admin-éditeur-visiteur)
10. [Déploiement](#10-déploiement)
11. [Annexe — référence des endpoints](#11-annexe--référence-des-endpoints)

> **Note de lecture.** Les figures sont écrites en **Mermaid** : elles se rendent
> automatiquement sur GitHub, dans VS Code (extension *Markdown Preview Mermaid*) et la
> plupart des visualiseurs Markdown.

---

## 1. Vue d'ensemble

La plateforme est une application web **client/serveur** découplée :

- un **frontend Angular** (SPA) qui ne contient aucune logique métier sensible ;
- une **API REST** ASP.NET Core qui porte toute la logique, la sécurité et l'accès aux données ;
- des **services d'infrastructure auto-hébergés** : PostgreSQL (données), MinIO (objets/vidéos),
  Hangfire + FFmpeg (traitement asynchrone), Nginx (frontal).

```mermaid
flowchart LR
    subgraph Client
        NAV["Navigateur<br/>(Visiteur / Éditeur / Admin)"]
    end

    NGINX["Nginx<br/>reverse proxy"]

    subgraph Backend["Backend auto-hébergé"]
        API["API REST<br/>ASP.NET Core 8 · JWT"]
        WORKER["Worker Hangfire<br/>+ FFmpeg 6"]
        PG[("PostgreSQL 16<br/>EF Core · full-text")]
        MINIO[("MinIO (S3)<br/>originals · hls")]
    end

    NAV -->|HTTPS| NGINX
    NGINX -->|"/api, /hls"| API
    NGINX -->|SPA statique| NAV
    API <-->|EF Core| PG
    API <-->|S3| MINIO
    API -.->|enfile un job| WORKER
    WORKER <-->|EF Core| PG
    WORKER <-->|S3| MINIO
```

**Principes directeurs**

| Principe | Mise en œuvre |
|---|---|
| Auto-hébergement total | Aucune dépendance externe ; toute brique tourne sur l'infrastructure MAP. |
| Séparation des responsabilités | Découpage backend Domain / Application / Infrastructure / Api. |
| Sécurité par rôle | ASP.NET Core Identity (hash), JWT Bearer, RBAC (`[Authorize(Roles=…)]`). |
| Traitement asynchrone | Le transcodage (lourd) est déporté dans une file Hangfire + worker FFmpeg. |
| Diffusion adaptative | HLS multi-débit (360p/720p/1080p) servi via un proxy contrôlé. |
| Internationalisation | Interface bilingue FR/AR avec mise en page **RTL**. |

---

## 2. Composants applicatifs

### 2.1 Carte des composants

```mermaid
flowchart TB
    subgraph FE["Frontend — Angular 17 (standalone, Signals)"]
        PUB["Espace Visiteur<br/>catalogue · lecteur HLS"]
        EDI["Espace Éditeur<br/>upload · métadonnées · publication"]
        ADM["Espace Admin<br/>utilisateurs · stats · audit · config"]
        CORE["core/ : AuthStore · intercepteur JWT · gardes · i18n FR/AR"]
    end

    subgraph API["API — ASP.NET Core 8"]
        direction TB
        CTRL["Controllers (mince)"]
        APP["Application : DTOs, interfaces, règles"]
        INFRA["Infrastructure : services + EF Core + MinIO + FFmpeg"]
        DOM["Domain : entités & enums"]
        CTRL --> APP --> INFRA --> DOM
    end

    subgraph DATA["Données & médias"]
        PG[("PostgreSQL")]
        MINIO[("MinIO")]
        HANG["Hangfire (jobs)"]
    end

    FE -->|"REST /api/v1 (JWT)"| CTRL
    INFRA --> PG
    INFRA --> MINIO
    INFRA --> HANG
```

### 2.2 Frontend (Angular)

Application **standalone** (sans NgModules), état applicatif via **Signals**, style **Tailwind**
(thème « Newsroom » : bleu encre + rouge MAP). Trois espaces distincts, chargés en *lazy* :

- **`core/`** — `AuthStore` (jeton JWT + utilisateur + rôles, persistance `localStorage`),
  **intercepteur HTTP** (ajoute `Authorization: Bearer`, déconnecte sur `401`), **gardes**
  (`authGuard`, `editorGuard`, `adminGuard`), **i18n** maison (service Signals + pipe `| t`,
  bascule FR/AR + `dir="rtl"`).
- **`shared/ui/`** — composants réutilisables (badge de statut, pagination, barre de recherche,
  vignette vidéo, spinner…).
- **`features/`** — `public` (catalogue, détail + lecteur), `editor` (tableau de bord, upload,
  édition), `auth` (login/register), `admin` (stats, utilisateurs, catégories, audit, config).
- **`layouts/`** — `PublicShell` (barre haute), `EditorShell` et `AdminShell` (barres latérales).

| Espace public (FR, LTR) | Espace public (AR, RTL) |
|---|---|
| ![Catalogue FR](images/ui-catalogue-fr.png) | ![Catalogue AR RTL](images/ui-catalogue-ar-rtl.png) |

### 2.3 Reverse proxy (Nginx)

Point d'entrée unique. Sert la SPA, route `/api/*` vers l'API et `/hls/*` vers le proxy de
diffusion. Permet de masquer la topologie interne et de centraliser TLS/compression.

### 2.4 API ASP.NET Core — couches

```mermaid
flowchart LR
    A["Api<br/>controllers, auth, ProblemDetails"] --> B["Application<br/>DTOs, interfaces, exceptions"]
    B --> C["Infrastructure<br/>services concrets, EF Core, MinIO, FFmpeg"]
    C --> D["Domain<br/>entités, enums, règles invariantes"]
    B -. dépend des contrats .- D
```

- **Domain** — entités (`Video`, `User`, `Comment`, `Like`, `VideoRendition`, `TranscodeJob`…)
  et enums (`VideoStatus`, `TranscodeJobStatus`). Sans dépendance technique.
- **Application** — contrats (`IAuthService`, `ICatalogService`, `IStreamingService`,
  `IEngagementService`, `IUploadService`, `IAdminService`…), DTOs, exceptions métier.
- **Infrastructure** — implémentations : services, `AppDbContext` (EF Core), `MinioObjectStorage`,
  `FfmpegVideoTranscoder`, `TranscodePipeline`, `JwtTokenGenerator`.
- **Api** — contrôleurs **minces** (controllers → services), gestion d'erreur normalisée
  (`ProblemDetails`), configuration JWT/Hangfire/Swagger.

### 2.5 Services métier (Infrastructure)

| Service | Rôle |
|---|---|
| `AuthService` + `JwtTokenGenerator` | Inscription, connexion, émission/validation du JWT. |
| `CatalogService` | Recherche full-text, détail, publication/archivage, « mes vidéos ». |
| `UploadService` | Réception des chunks, validation de complétude. |
| `TranscodePipeline` + `FfmpegVideoTranscoder` | Transcodage HLS multi-débit. |
| `StreamingService` | Renditions + proxy HLS avec contrôle d'accès. |
| `EngagementService` | Commentaires, likes, partages, résumé d'engagement. |
| `AnalyticsService` | Télémétrie de visionnage, statistiques globales. |
| `AuditService` | Journalisation des actions sensibles. |
| `AdminService` | Utilisateurs/rôles, configuration système. |
| `CategoryService` / `TagService` | Référentiels du catalogue. |

### 2.6 Données & médias

- **PostgreSQL 16** — toutes les données relationnelles ; recherche **full-text** (`tsvector`)
  sur titre/description/tags ; pas de moteur de recherche externe.
- **MinIO (S3)** — deux buckets : `originals` (fichiers source téléversés, par chunks) et
  `hls` (segments `.ts` + playlists `.m3u8` générés).
- **Hangfire** — file de jobs persistée en PostgreSQL (pas de Redis requis en OSS) ; un **worker**
  dédié exécute le transcodage FFmpeg, avec **retry** automatique en cas d'échec.

---

## 3. Modèle de données

```mermaid
erDiagram
    USER ||--o{ USERROLE : possède
    ROLE ||--o{ USERROLE : attribué
    USER ||--o{ VIDEO : "propriétaire (Éditeur)"
    CATEGORY ||--o{ VIDEO : classe
    VIDEO ||--o{ VIDEOTAG : étiquette
    TAG ||--o{ VIDEOTAG : utilisé
    VIDEO ||--o{ VIDEORENDITION : "rendus HLS"
    VIDEO ||--o| TRANSCODEJOB : "job de transcodage"
    VIDEO ||--o{ COMMENT : commentée
    VIDEO ||--o{ LIKE : aimée
    VIDEO ||--o{ SHARE : partagée
    VIDEO ||--o{ VIEWEVENT : visionnée
    USER ||--o{ COMMENT : écrit
    USER ||--o{ LIKE : "aime (unique)"

    VIDEO {
        guid Id
        string Title
        string Slug
        enum Status
        tsvector SearchVector
        double DurationSeconds
        datetime PublishedAt
    }
    VIDEORENDITION {
        string Resolution
        int Bitrate
        string ManifestKey
    }
    TRANSCODEJOB {
        enum Status
        int Progress
        int Attempts
        string Error
    }
    LIKE {
        guid VideoId
        guid UserId
    }
```

Entités complémentaires : **`AuditLog`** (traçabilité : acteur, action, entité, horodatage) et
**`SystemSetting`** (configuration clé/valeur). Contrainte forte : un **like est unique** par
couple (vidéo, utilisateur).

---

## 4. Téléversement (upload chunké)

Le fichier source est découpé côté navigateur en **tranches de 5 Mo** envoyées une à une. Chaque
chunk est stocké tel quel dans MinIO (`originals/{videoId}/parts/{index}`). La finalisation
vérifie que **toutes** les tranches sont présentes, puis enfile le transcodage.

```mermaid
sequenceDiagram
    autonumber
    actor ED as Éditeur (navigateur)
    participant API as API
    participant MIN as MinIO (originals)
    participant HQ as Hangfire

    ED->>API: POST /videos {titre, description}
    API-->>ED: { id } (statut Draft)
    loop pour chaque tranche de 5 Mo
        ED->>API: POST /videos/{id}/upload/chunk?index=N (octets bruts)
        API->>MIN: PUT originals/{id}/parts/N
        API-->>ED: { received: [indices reçus] }
    end
    ED->>API: POST /videos/{id}/upload/complete?total=T
    API->>MIN: vérifie que les T tranches existent
    API->>HQ: Enqueue TranscodeVideoJob(id)
    API-->>ED: { id, status: "Processing" }
    Note over ED,API: redirection vers l'écran d'édition des métadonnées
```

**Pourquoi par chunks ?** Robustesse (reprise possible), gros fichiers sans limite de requête,
et découplage de l'étape lourde (le transcodage) qui devient asynchrone.

| Écran de téléversement (Éditeur) |
|---|
| ![Upload éditeur](images/ui-upload-editeur.png) |

---

## 5. Encodeur vidéo & pipeline de transcodage

### 5.1 Principe

Le **worker** exécute `TranscodePipeline`, qui s'appuie sur **FFmpeg/ffprobe** (`FfmpegVideoTranscoder`).
Pour chaque vidéo, on génère un **HLS multi-débit** : plusieurs « échelons » (rungs) de qualité,
chacun avec sa playlist `.m3u8` et ses segments `.ts`, plus un **manifeste maître** qui les liste.

L'**échelle** est adaptative : on ne transcode **jamais au-dessus** de la résolution source
(pas d'upscaling), mais on garde toujours au moins le plus bas échelon.

| Échelon | Hauteur | Codecs | Usage |
|---|---|---|---|
| 360p | 360 | H.264 (main) + AAC | réseau lent / mobile |
| 720p | 720 | H.264 (main) + AAC | qualité standard |
| 1080p | 1080 | H.264 (main) + AAC | haute définition |

> Chaque échelon est encodé en `libx264 -profile:v main -preset veryfast`, audio `aac`,
> segments `-hls_time` (VOD). Le manifeste maître déclare `BANDWIDTH`, `RESOLUTION` et `CODECS`
> pour permettre au lecteur de choisir automatiquement la qualité (ABR).

### 5.2 Étapes du pipeline

```mermaid
flowchart TD
    A["Job Hangfire : TranscodeVideoJob(videoId)"] --> B["Statut → Processing<br/>Job → Running, Attempts++"]
    B --> C["Nettoyage des sorties d'une tentative précédente<br/>(idempotence des retries)"]
    C --> D["Téléchargement + concaténation<br/>des chunks depuis MinIO (originals)"]
    D --> E["ffprobe : largeur, hauteur, durée"]
    E --> F["Sélection de l'échelle HLS<br/>(rungs ≤ hauteur source)"]
    F --> G{"Pour chaque échelon"}
    G -->|FFmpeg| H["Transcodage → index.m3u8 + seg_###.ts"]
    H --> I["Upload des segments + playlist<br/>vers MinIO (hls/{id}/{rung}/...)"]
    I --> J["Ajout d'une ligne au manifeste maître<br/>+ VideoRendition en base"]
    J --> G
    G -->|fini| K["Écriture master.m3u8 dans MinIO"]
    K --> L["Statut → Ready · durée enregistrée<br/>Job → Succeeded"]
    L --> M["Suppression des chunks originaux"]

    B -. exception .-> X["Statut → Failed<br/>Job → Failed + Error<br/>↻ retry Hangfire"]
    H -. exception .-> X
```

### 5.3 Arborescence MinIO produite

```
hls/{videoId}/
├── master.m3u8            ← manifeste maître (liste les échelons)
├── 360p/
│   ├── index.m3u8
│   └── seg_000.ts, seg_001.ts, …
├── 720p/
│   ├── index.m3u8
│   └── seg_000.ts, …
└── 1080p/
    ├── index.m3u8
    └── seg_000.ts, …
```

En cas d'échec, l'exception est relancée : **Hangfire** réessaie le job (idempotent grâce au
nettoyage initial), et la vidéo passe en **`Failed`** avec le message d'erreur enregistré.

---

## 6. Streaming HLS

### 6.1 Lecture adaptative

Le lecteur (**hls.js**, développé en interne) charge d'abord le **manifeste maître**, puis choisit
dynamiquement l'échelon adapté au débit réseau (ABR), et enchaîne le téléchargement des segments
`.ts`. Les segments ne sont **jamais servis en direct depuis MinIO** : ils passent par un **proxy**
de l'API qui applique le contrôle d'accès.

```mermaid
sequenceDiagram
    autonumber
    actor V as Visiteur (lecteur hls.js)
    participant API as API (/videos/{id})
    participant SVC as StreamingService
    participant MIN as MinIO (hls)

    V->>API: GET /videos/{id}/stream
    API-->>V: { manifestUrl: "/videos/{id}/hls/master.m3u8", renditions[] }
    V->>API: GET /videos/{id}/hls/master.m3u8
    API->>SVC: OpenHlsAsync(id, "master.m3u8")
    SVC->>SVC: contrôle d'accès (Published ? sinon propriétaire/Admin)
    SVC->>MIN: GET hls/{id}/master.m3u8
    MIN-->>V: manifeste maître
    loop segments selon le débit (ABR)
        V->>API: GET /videos/{id}/hls/720p/seg_007.ts
        API->>SVC: OpenHlsAsync(...) + contrôle d'accès
        SVC->>MIN: GET hls/{id}/720p/seg_007.ts
        MIN-->>V: segment (Range supporté)
    end
```

### 6.2 Sécurité du proxy

`StreamingService.OpenHlsAsync` :

- **rejette** les chemins suspects (`..`, chemin absolu) — protection contre la traversée ;
- **vérifie la visibilité** : une vidéo non `Published` n'est accessible qu'à son **propriétaire**
  ou à un **Admin** ; sinon `404` ;
- renvoie le bon `Content-Type` (`application/vnd.apple.mpegurl` pour `.m3u8`, `video/mp2t` pour `.ts`)
  et active le **support des Range requests** (seek/buffering).

---

## 7. Cycle de vie d'une vidéo (transversal)

Le statut d'une vidéo orchestre l'ensemble des composants (upload, transcodage, diffusion,
engagement). C'est l'axe **transversal** du système.

```mermaid
stateDiagram-v2
    [*] --> Draft : POST /videos (Éditeur)
    Draft --> Processing : upload/complete → job enfilé
    Processing --> Ready : transcodage HLS réussi
    Processing --> Failed : erreur FFmpeg
    Failed --> Processing : retry Hangfire
    Ready --> Published : POST /videos/{id}/publish
    Published --> Archived : POST /videos/{id}/archive
    Archived --> Published : republication
    Published --> [*]
```

| Statut | Signification | Visible au catalogue public ? |
|---|---|---|
| **Draft** | Créée, fichier en cours de téléversement | Non |
| **Processing** | Transcodage en cours (worker FFmpeg) | Non |
| **Ready** | HLS prêt, publiable par l'éditeur | Non (propriétaire/Admin seulement) |
| **Published** | Diffusée | **Oui** |
| **Archived** | Retirée du catalogue | Non |
| **Failed** | Échec du pipeline (+ retry) | Non |

Le **`TranscodeJob`** suit ce cycle côté traitement (`Running → Succeeded`/`Failed`, `Attempts`,
`Error`), permettant à l'éditeur de suivre l'état dans son tableau de bord.

---

## 8. Commentaires & likes (engagement)

L'engagement (`EngagementService`) couvre **commentaires**, **likes** et **partages**. Règle
d'accès : le **visionnage est anonyme**, mais **commenter / liker / partager exige une
authentification** (`[Authorize]`). Toutes les opérations vérifient d'abord la **visibilité** de
la vidéo (publiée, ou propriétaire/Admin).

### 8.1 Ajout d'un commentaire

```mermaid
sequenceDiagram
    autonumber
    actor U as Utilisateur connecté
    participant API as API (/videos/{id}/comments)
    participant SVC as EngagementService
    participant PG as PostgreSQL

    U->>API: POST /videos/{id}/comments { body } (Bearer)
    API->>SVC: AddCommentAsync(id, body, userId)
    SVC->>SVC: vidéo visible ? · corps non vide ?
    SVC->>PG: INSERT Comment
    SVC->>PG: SELECT displayName de l'auteur
    SVC-->>U: { id, body, auteur, date }
```

- **Liste** paginée (20 / page), triée par date décroissante, en **excluant** les commentaires
  modérés (`IsModerated`).
- **Suppression** : réservée à **l'auteur** du commentaire ou à un **Admin** (sinon `403`).

### 8.2 Like / Unlike (idempotent et unique)

```mermaid
flowchart TD
    A["POST /videos/{id}/likes (Bearer)"] --> B{"like déjà présent<br/>pour (vidéo, utilisateur) ?"}
    B -->|non| C["INSERT Like"]
    B -->|oui| D["aucune action (idempotent)"]
    C --> E["renvoie le résumé : likeCount, commentCount,<br/>shareCount, likedByMe = true"]
    D --> E
    F["DELETE /videos/{id}/likes"] --> G["supprime le like s'il existe"]
    G --> H["résumé : likedByMe = false"]
```

La contrainte d'**unicité (VideoId, UserId)** en base garantit qu'un utilisateur ne peut liker
qu'une fois. `GetSummaryAsync` renvoie les compteurs **et** l'indicateur `likedByMe` pour piloter
l'état du bouton côté UI. Les **partages** incrémentent un compteur par canal (`Channel`).

---

## 9. Accès : Admin, Éditeur, Visiteur

### 9.1 Modèle RBAC

L'authentification repose sur **ASP.NET Core Identity** (hachage du mot de passe) et un **JWT**
signé contenant l'identité et les **rôles**. Chaque requête protégée est filtrée par
`[Authorize(Roles=…)]` côté API, et par des **gardes** côté Angular.

```mermaid
sequenceDiagram
    autonumber
    actor U as Utilisateur
    participant FE as Frontend (AuthStore)
    participant API as API (/auth)
    participant PG as PostgreSQL

    U->>FE: saisit e-mail + mot de passe
    FE->>API: POST /auth/login
    API->>PG: vérifie l'utilisateur + hash
    API-->>FE: { token JWT, roles[] }
    FE->>FE: stocke le jeton (localStorage) + rôles (Signals)
    Note over FE: l'intercepteur ajoute « Authorization: Bearer » à chaque appel
    FE->>API: GET /admin/stats (Bearer)
    API->>API: [Authorize(Roles="Admin")] valide le jeton + rôle
    API-->>FE: données (ou 401/403)
```

Côté frontend, le routage applique les gardes :

```mermaid
flowchart LR
    R["Route demandée"] --> G{"Garde"}
    G -->|"/ — public"| OK1["Accès anonyme autorisé"]
    G -->|"/studio · editorGuard"| C1{"connecté ET Éditeur/Admin ?"}
    G -->|"/admin · adminGuard"| C2{"connecté ET Admin ?"}
    C1 -->|oui| OK2["Espace Éditeur"]
    C1 -->|non| LOGIN1["→ /auth/login"]
    C2 -->|oui| OK3["Espace Admin"]
    C2 -->|non| LOGIN2["→ /auth/login"]
```

### 9.2 Capacités par rôle

| Capacité | Visiteur | Éditeur | Admin |
|---|:---:|:---:|:---:|
| Naviguer le catalogue, visionner (HLS) | ✅ (anonyme) | ✅ | ✅ |
| Commenter / liker / partager | 🔒 connecté | ✅ | ✅ |
| Téléverser une vidéo, éditer ses métadonnées | — | ✅ | ✅ |
| Publier / archiver une vidéo | — | ✅ (les siennes) | ✅ (toutes) |
| Voir « mes vidéos » + suivi de transcodage | — | ✅ | ✅ |
| Gérer les utilisateurs & rôles | — | — | ✅ |
| Gérer les catégories | — | — | ✅ |
| Statistiques globales (`/stats`) | — | — | ✅ |
| Journal d'audit (`/audit`) | — | — | ✅ |
| Configuration système (`/config`) | — | — | ✅ |

> Le compte **Administrateur** initial est créé automatiquement au démarrage de l'API (seed),
> puis l'Admin peut **promouvoir** d'autres comptes en Éditeur/Admin. Un nouvel inscrit reçoit
> par défaut le rôle **Visiteur**.

### 9.3 Espaces & coquilles

```mermaid
flowchart TB
    subgraph Visiteur["Espace Visiteur — barre haute"]
        VC["Catalogue · recherche · filtres"]
        VD["Détail vidéo · lecteur HLS · engagement"]
    end
    subgraph Editeur["Espace Éditeur — /studio (sidebar)"]
        ED1["Tableau de bord (mes vidéos + statuts)"]
        ED2["Téléverser"]
        ED3["Éditer · publier · archiver"]
    end
    subgraph Admin["Espace Admin — /admin (sidebar)"]
        AD1["Statistiques"]
        AD2["Utilisateurs & rôles"]
        AD3["Catégories"]
        AD4["Audit"]
        AD5["Configuration"]
    end
```

---

## 10. Déploiement

Orchestration via **Docker Compose** — pile 100 % auto-hébergée, aucun service externe.

```mermaid
flowchart TB
    subgraph Hote["Hôte Docker"]
        NG["nginx :80"]
        AP["api :8080"]
        WK["worker (--worker) + ffmpeg"]
        PGc[("postgres :5432")]
        RDc[("redis :6379")]
        MNc[("minio :9000/:9001")]
    end
    NG --> AP
    AP --> PGc
    AP --> MNc
    WK --> PGc
    WK --> MNc
    AP -. file de jobs .- WK
```

- **api** et **worker** partagent la même image (le runtime inclut **FFmpeg**) ; le worker démarre
  avec `--worker` et n'héberge que le serveur Hangfire.
- Au démarrage, l'API **applique les migrations** EF Core, **crée les buckets** MinIO et **initialise
  le compte administrateur**.
- La **vérification** repose sur des tests d'intégration à **conteneurs réels** (PostgreSQL + MinIO
  via Testcontainers) côté backend, et **Jest** côté frontend.

---

## 11. Annexe — référence des endpoints

Tous sous `/api/v1`. Erreurs normalisées via `ProblemDetails`.

| Domaine | Endpoints | Accès |
|---|---|---|
| **Auth** | `POST /auth/login` · `POST /auth/register` · `GET /auth/me` | Public / Bearer |
| **Catalogue** | `GET /videos` (q, categoryId, tag, page) · `GET /videos/{id}` | Anonyme |
| **Diffusion** | `GET /videos/{id}/stream` · `GET /videos/{id}/hls/{**path}` | Anonyme (si publiée) |
| **Édition** | `POST /videos` · `/upload/chunk` · `/upload/complete` · `PUT /videos/{id}` · `/publish` · `/archive` · `GET /videos/mine` | Éditeur |
| **Engagement** | `GET/POST/DELETE /videos/{id}/comments` · `POST/DELETE /videos/{id}/likes` · `POST /videos/{id}/shares` · `GET /videos/{id}/engagement` · `POST /videos/{id}/views` | Anonyme (lecture) / Connecté (écriture) |
| **Admin** | `GET /stats` · `GET /audit` · `GET/PUT /config` | Admin |
| **Utilisateurs** | `GET /users` · `PUT /users/{id}` · `POST /users/{id}/roles` · `DELETE /users/{id}/roles/{role}` | Admin |
| **Référentiels** | `GET /categories` · `POST/PUT/DELETE /categories/{id}` · `GET /tags` | Public (lecture) / Admin (écriture) |

---

*Document généré pour le Projet de Fin d'Études — Plateforme de diffusion vidéo MAP. Les figures
Mermaid et les schémas reflètent l'implémentation réelle (services, endpoints, pipeline) du dépôt.*
