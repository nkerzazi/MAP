# Phase 4 — Diffusion & viewer — Design

> Conception de la diffusion HLS et du lecteur (Phase 4 du [découpage](2026-06-21-plateforme-streaming-interne-design.md), §12).
> Dépend de la Phase 2 (objets HLS dans MinIO) et de la Phase 3 (statut `Published`, visibilité).
> Date : 2026-06-22.

## 1. Objectif et périmètre

Transformer le stub `GET /videos/{id}/stream` en **diffusion HLS réelle** : un proxy de
streaming sert le manifeste et les segments depuis MinIO, et le composant lecteur `hls.js`
existant les consomme. Inclut la correction du **manifeste maître** (dette Phase 2).

**Dans le périmètre** : proxy HLS API, `/stream` réel, `IStreamingService`, fix
`RESOLUTION`/`CODECS` du master playlist, branchement minimal du lecteur Angular.

**Hors périmètre** : télémétrie `/views` (Phase 6), build complet de l'app Angular,
sous-titres, DRM, service direct via Nginx.

## 2. Décisions d'architecture

- **Proxy de streaming via l'API** : MinIO reste interne ; les références relatives du
  manifeste se résolvent contre l'endpoint. Visionnage anonyme pour les vidéos `Published`.
- **Content-type par extension** (`.m3u8` → `application/vnd.apple.mpegurl`, `.ts` →
  `video/mp2t`) — pas d'appel stat MinIO.
- **Visibilité** identique au détail : `Published` → tous ; non publiée → propriétaire/Admin.
  Vérifiée à chaque requête (1 hit DB/segment ; acceptable pour un outil interne).
- **Manifeste maître corrigé** : `RESOLUTION={largeur}x{hauteur}` (largeur sondée) + `CODECS`.

## 3. API REST

| Méthode | Route | Comportement |
|---|---|---|
| `GET` | `/api/v1/videos/{id}/stream` | `{ id, manifestUrl, renditions[] }` ; `manifestUrl = "/api/v1/videos/{id}/hls/master.m3u8"` ; renditions depuis `VideoRendition`. Non visible → 404. |
| `GET` | `/api/v1/videos/{id}/hls/{**path}` | Streame `hls/{id}/{path}` depuis MinIO avec content-type par extension, `enableRangeProcessing`. `path` contenant `..` → 400. Non visible → 404. Anonyme autorisé pour `Published`. |

## 4. Architecture logicielle

- **Application** : `IStreamingService` + DTO `StreamInfo(Guid Id, string ManifestUrl, IReadOnlyList<RenditionInfo> Renditions)` (réutilise `RenditionInfo` de `Catalog`). Le service ne construit **pas** d'URL (concern web) : `GetStreamRenditionsAsync` renvoie les renditions, le contrôleur compose `manifestUrl`.
  - `Task<IReadOnlyList<RenditionInfo>> GetRenditionsAsync(Guid id, Guid? userId, bool isAdmin, ct)` — visibilité + renditions (sinon `VideoNotFoundException`).
  - `Task<HlsObject> OpenHlsAsync(Guid id, string path, Guid? userId, bool isAdmin, ct)` — `HlsObject(Stream Content, string ContentType)` après visibilité + validation du path.
- **Infrastructure** : `StreamingService : IStreamingService` (`AppDbContext` + `IObjectStorage`). Réutilise la règle de visibilité (Published || owner || admin). `OpenHlsAsync` rejette les `path` vides, absolus ou contenant `..` (lève `ArgumentException` → 400).
- **Api** : `VideosController` — remplace le stub `Stream` ; ajoute l'action proxy `Hls` renvoyant `File(stream, contentType, enableRangeProcessing: true)`. Exceptions `VideoNotFoundException` → 404, `ArgumentException` → 400.

`HlsObject` et `StreamInfo` sont de petits records dédiés.

## 5. Correction du manifeste maître (pipeline Phase 2)

- `IVideoTranscoder.ProbeAsync` → `Task<(int Width, int Height, double DurationSeconds)>`.
- `FfmpegVideoTranscoder.ProbeAsync` : `ffprobe ... -show_entries stream=width,height` (parse les deux).
- `TranscodePipeline` : pour chaque échelon, largeur = `arrondi_pair(sourceWidth * rungHeight / sourceHeight)` ; émet `#EXT-X-STREAM-INF:BANDWIDTH=...,RESOLUTION={w}x{rungHeight},CODECS="avc1.640028,mp4a.40.2"`.
- Mettre à jour les doublures de test (`TranscodePipelineTests` `FakeTranscoder`/`ThrowingTranscoder`, `FfmpegSmokeTests`) pour la nouvelle signature de `ProbeAsync`.

## 6. Frontend (branchement minimal)

- `frontend/src/app/viewer/hls-player.component.ts` existe (prend `manifestUrl`, hls.js + fallback Safari) — conservé.
- Ajout de `frontend/src/app/viewer/viewer-page.component.ts` : appelle `GET /api/v1/videos/{id}/stream`, récupère `manifestUrl`, et rend `<app-hls-player [manifestUrl]="...">`. **Non intégré à une app Angular runnable** (toolchain Node non confirmée) — code correct et documenté.

## 7. Gestion des erreurs

| Cas | Code |
|---|---|
| Vidéo non visible (non publiée pour anonyme) | 404 |
| `path` HLS invalide (`..`, absolu, vide) | 400 |
| Objet HLS absent dans MinIO | 404 |

## 8. Tests (TDD)

**StreamingService** (Postgres + MinIO Testcontainers) :
- seed vidéo `Published` + objets HLS (`master.m3u8`, `360p/index.m3u8`, segment) ; `OpenHlsAsync("master.m3u8")` → contenu + `application/vnd.apple.mpegurl` ;
- vidéo non publiée → `OpenHlsAsync`/`GetRenditionsAsync` lèvent `VideoNotFoundException` (sauf propriétaire) ;
- `path` avec `..` → `ArgumentException`.

**HTTP** (`WebApplicationFactory` + Postgres + MinIO) :
- `GET /videos/{id}/hls/master.m3u8` (Published) → 200 + content-type m3u8 ;
- vidéo non publiée → 404 ; `GET /videos/{id}/stream` → `manifestUrl` attendu + renditions.

**Pipeline** : `ProbeAsync` renvoie la largeur ; après transcodage (fake), `master.m3u8`
contient `RESOLUTION={w}x{h}` et `CODECS=`.

## 9. Hors périmètre / YAGNI

Télémétrie de visionnage, app Angular complète, adaptive-bitrate côté serveur, cache de
visibilité, sous-titres/pistes multiples, signed cookies — phases ultérieures.
