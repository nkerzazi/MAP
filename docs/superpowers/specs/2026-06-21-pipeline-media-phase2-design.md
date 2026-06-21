# Phase 2 — Pipeline média : upload → MinIO → Hangfire/FFmpeg → HLS — Design

> Conception du pipeline média (Phase 2 du [découpage de la spec principale](2026-06-21-plateforme-streaming-interne-design.md), §12).
> Dépend du RBAC de la Phase 1 ([auth Identity + JWT](2026-06-21-auth-identity-jwt-rbac-design.md)) pour le rôle `Editeur`.
> Date : 2026-06-21.

## 1. Objectif et périmètre

Permettre à un **Éditeur** de téléverser une vidéo source (potentiellement volumineuse)
en **chunks**, de la stocker dans **MinIO**, puis de la transcoder en **HLS multi-débit**
via un **worker Hangfire + FFmpeg**, jusqu'au statut `Ready`.

**Dans le périmètre** : init vidéo, upload chunké, stockage objet, job de transcodage,
génération HLS adaptative, machine à états `Draft → Processing → Ready/Failed`, retries.

**Hors périmètre (Phase 4)** : diffusion/lecture HLS (URLs présignées, endpoint `/stream`,
lecteur hls.js). La Phase 2 **produit et stocke** les rendus, elle ne les sert pas.

## 2. Décisions d'architecture

- **Upload chunké proxifié par l'API** : MinIO reste 100 % interne (jamais exposé au client).
  Le SDK Minio .NET n'exposant pas le multipart bas niveau (`UploadPart`/`ListParts`),
  **chaque chunk est stocké comme un objet MinIO distinct** ; la reconstitution se fait
  par concaténation côté worker. L'API reste **sans état** (aucun disque local requis).
- **Ladder HLS adaptative, sans upscaling** : on ne génère que les échelons ≤ résolution source.
- **Worker dédié** : l'API enfile seulement ; un process worker séparé exécute le transcodage
  (isole la charge CPU FFmpeg de l'API).
- **Reprise** : 3 tentatives Hangfire (backoff) puis `Failed`.
- **Stockage Hangfire = PostgreSQL** (`Hangfire.PostgreSql`, déjà référencé).
  > Divergence assumée vs CLAUDE.md (« Hangfire sur Redis ») : Hangfire OSS n'a pas de
  > storage Redis gratuit. Tout reste auto-hébergé ; Redis demeure disponible pour du
  > cache ultérieur. À refléter dans la note mémoire du dépôt.

## 3. Upload chunké

Endpoints, tous `[Authorize(Roles="Editeur")]`, sous `api/v1/videos` :

| Méthode | Route | Rôle |
|---|---|---|
| `POST` | `/` | Crée la vidéo (`Draft`), renvoie `{ id }`. Body : titre + métadonnées minimales. |
| `POST` | `/{id}/upload/chunk?index=N` | Body = octets du chunk N. Streamé directement dans MinIO `originals/{videoId}/parts/{N:000000}`. **Idempotent** (re-POST = reprise). Renvoie la liste des index reçus. |
| `POST` | `/{id}/upload/complete?total=K` | Vérifie la présence des K parts ; fixe `Video.OriginalKey = originals/{videoId}/parts/` ; passe en `Processing` ; **enfile** `TranscodeVideoJob`. |

- Validation : taille de chunk max configurable ; `index` contigus 0..K-1 ; `total` cohérent.
- Concurrence : un upload en cours par vidéo (état dérivé du statut `Draft`).
- Les parts sont des plages d'octets contiguës du fichier source ; leur concaténation
  ordonnée **reconstitue exactement** l'original (pas de remux nécessaire).

## 4. Transcodage (worker)

`TranscodeVideoJob(Guid videoId)` (méthode Hangfire) délègue à **`ITranscodePipeline`**
(interface dans `Application` ; implémentation dans `Infrastructure` avec `AppDbContext`,
`IObjectStorage`, `IVideoTranscoder`) :

1. Charge la vidéo ; `Video → Processing`, crée/maj `TranscodeJob → Running` (incrémente `Attempts`).
2. Liste et télécharge `originals/{videoId}/parts/*` dans l'ordre, **concatène** vers un
   fichier source temporaire local au worker.
3. `ffprobe` → résolution source + `DurationSeconds`.
4. Sélectionne les échelons applicables parmi **360p / 720p / 1080p** tels que
   `hauteur_échelon ≤ hauteur_source` (au moins le plus bas si source < 360p).
5. Pour chaque échelon : `ffmpeg` produit `index.m3u8` + segments `.ts`, uploadés dans
   `hls/{videoId}/{res}/…` ; crée une `VideoRendition` (Resolution, Bitrate, ManifestKey).
6. Génère le manifeste maître `hls/{videoId}/master.m3u8` (références relatives aux variantes).
7. `Video → Ready`, `TranscodeJob → Succeeded` ; supprime le temp local **et** `parts/*`.
8. Toute exception remonte → **retry Hangfire ×3** ; à l'échec définitif :
   `Video → Failed`, `TranscodeJob → Failed` + `Error` renseigné (notifiable à l'éditeur).

Implémentations `Infrastructure` :
- `MinioObjectStorage : IObjectStorage` (SDK Minio) — buckets `originals` / `hls` créés au démarrage (idempotent).
- `FfmpegVideoTranscoder : IVideoTranscoder` — shell-out `ffmpeg` / `ffprobe` via `System.Diagnostics.Process`,
  répertoire de travail temporaire, capture des erreurs stderr.

### Paramètres FFmpeg (indicatif)

| Échelon | Hauteur | Bitrate vidéo cible | Audio |
|---|---|---|---|
| 360p | 360 | ~800 kbps | AAC 96 kbps |
| 720p | 720 | ~2 500 kbps | AAC 128 kbps |
| 1080p | 1080 | ~5 000 kbps | AAC 128 kbps |

H.264 (`libx264`), segments HLS ~6 s, `-hls_playlist_type vod`. Valeurs ajustables en configuration.

## 5. Topologie process (`Program.cs`)

- **Mode API** (défaut) : `AddHangfire(...UsePostgreSqlStorage...)` en **client** (enqueue),
  Kestrel + controllers. Pas de serveur Hangfire.
- **Mode worker** (`dotnet … --worker`) : `AddHangfireServer` + enregistrement de
  `ITranscodePipeline` / `IVideoTranscoder`, **sans** Kestrel. FFmpeg présent dans l'image.
- Branche `--worker` à ajouter dans `Program.cs` (aiguillage en tête de `Main`).

## 6. Infrastructure

- **Image worker** : le `Dockerfile` (partagé API/worker) doit installer **FFmpeg 6**
  (`apt-get install ffmpeg`) dans l'étape runtime du worker.
- **Configuration** : section `Transcoding` (tailles de chunk, échelons, bitrates, segment duration),
  `Hangfire` (connexion Postgres = `ConnectionStrings:Postgres`).

## 7. Modèle de données

Réutilise l'existant (aucune entité nouvelle indispensable) :
- `Video.OriginalKey`, `Video.Status`, `Video.DurationSeconds`.
- `VideoRendition` (Resolution, Bitrate, ManifestKey, VideoId).
- `TranscodeJob` (VideoId, Status, Progress, Error, Attempts, CreatedAt) — déjà présent ;
  ajout possible d'une mise à jour de `Progress` (0–100) pendant le transcodage (optionnel, YAGNI si non requis).

## 8. Gestion des erreurs

- Erreurs d'upload (index manquant, vidéo absente, mauvais statut) → `ProblemDetails` (400/404/409).
- Échec FFmpeg/MinIO dans le job → exception → retry Hangfire → `Failed` après épuisement.
- Nettoyage temp garanti (`try/finally`) même en cas d'échec.

## 9. Tests (TDD)

- **Orchestration** (Testcontainers PostgreSQL + MinIO ; `IVideoTranscoder` *fake*) :
  - upload de plusieurs chunks → objets `parts/*` présents ; `complete` → `Processing` + job enfilé ;
  - le pipeline fait `Processing → Ready`, crée les `VideoRendition` attendues, supprime les parts ;
  - échec simulé → `Failed` + `TranscodeJob.Error` après retries.
- **FFmpeg e2e** (test séparé, **skippé si `ffmpeg` absent** du runner) :
  - clip minuscule généré (`ffmpeg testsrc`) → `master.m3u8` + segments réels produits et probables.
- **Unitaire** : sélection adaptative du ladder (source 480p → 360p seul ; 4K → 360/720/1080) ;
  ordre de concaténation des parts.

## 10. Hors périmètre / YAGNI

Diffusion HLS et lecteur (Phase 4), notifications temps réel à l'éditeur, transcodage GPU,
sous-titres/pistes multiples, vignettes/poster auto, barre de progression fine — phases ultérieures.
