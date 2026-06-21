# CLAUDE.md

Guide pour travailler sur ce dépôt. Lis-le avant toute modification.

## Projet

**Plateforme de gestion et de diffusion de contenus vidéo** pour la **MAP (Maghreb
Arabe Presse)**. Solution interne **entièrement auto-hébergée** : téléversement,
traitement, stockage et diffusion de vidéos via streaming HLS, **sans aucun service
externe** (pas de YouTube/Vimeo, pas de cloud public, pas de CDN tiers).

Projet de Fin d'Études — Youssra Zounaki (EHEI Oujda / stage MAP Rabat).
Conception détaillée : [docs/superpowers/specs/2026-06-21-plateforme-streaming-interne-design.md](docs/superpowers/specs/2026-06-21-plateforme-streaming-interne-design.md).

## Stack

- **Backend** : ASP.NET Core 8 Web API (C# 12), découpage Domain/Application/Infrastructure/Api.
- **ORM / DB** : Entity Framework Core 8 + **PostgreSQL 16**.
- **Jobs async** : Hangfire (file de transcodage) sur **Redis**.
- **Stockage objet** : **MinIO** (S3-compatible, auto-hébergé) — vidéos + segments HLS.
- **Traitement vidéo** : **FFmpeg 6** → **HLS** multi-débit (360p/720p/1080p).
- **Frontend** : Angular 17+ (app unique, modules `admin` / `editor` / `viewer`).
- **Lecteur** : hls.js, développé en interne, exposé via API.
- **Reverse proxy** : Nginx. **Orchestration** : Docker Compose.
- **Auth** : ASP.NET Core Identity + JWT, RBAC (Admin / Éditeur / Visiteur).

> Note mémoire : la stack diverge du rapport V3 sur deux points à mettre à jour —
> PostgreSQL (au lieu de SQL Server) et MinIO (au lieu du système de fichiers).
> La correspondance MVC est conservée : Vue = Angular, Contrôleur = controllers/services,
> Modèle = entités EF Core.

## Architecture (résumé)

Angular SPA → API REST (JWT) → PostgreSQL ; MinIO pour les médias ; Hangfire+FFmpeg
pour le pipeline ; Nginx en frontal pour la diffusion HLS. Détails et diagrammes dans
la spec.

## Rôles et règles d'accès

- **Admin** : utilisateurs, rôles, configuration système, statistiques globales, audit.
- **Éditeur** : upload, édition des métadonnées, publication/archivage, recherche, indexation.
- **Visiteur** : navigation du catalogue, visionnage. **Visionnage anonyme autorisé** ;
  **authentification requise** pour commenter, liker, partager.

## Cycle de vie d'une vidéo

`Draft → Processing → Ready → Published → Archived` (+ `Failed` si le pipeline échoue,
avec retry Hangfire).

## Structure du dépôt

```
backend/   ASP.NET Core (src/ : Domain, Application, Infrastructure, Api ; tests/)
frontend/  Angular 17 (src/app/ : core, admin, editor, viewer, shared)
infra/     Nginx et configuration d'infrastructure
docs/      Conception et spécifications
docker-compose.yml  Pile complète auto-hébergée
```

## Commandes de développement

```bash
# Pile complète (Postgres, Redis, MinIO, API, worker, Nginx)
docker compose up -d

# Backend
cd backend && dotnet build
dotnet run --project src/MediaPlatform.Api
dotnet ef migrations add <Nom> --project src/MediaPlatform.Infrastructure --startup-project src/MediaPlatform.Api
dotnet ef database update --project src/MediaPlatform.Infrastructure --startup-project src/MediaPlatform.Api
dotnet test

# Frontend
cd frontend && npm install
npm start          # serveur de dev Angular
npm test           # tests unitaires (Jest)
npm run e2e        # tests e2e (Cypress)
```

## Conventions

- **API** versionnée sous `/api/v1`, erreurs normalisées via `ProblemDetails`.
- **Backend** : la logique métier vit dans `Application` ; `Api` reste mince
  (controllers → services). Pas d'accès EF direct depuis les controllers.
- **Async/await** systématique pour I/O (DB, MinIO, FFmpeg).
- **Pas de dépendance à un service externe** — toute nouvelle dépendance doit être
  auto-hébergeable. C'est une contrainte forte du projet.
- **Tests** : xUnit + Testcontainers côté backend ; Jest + Cypress côté frontend.
- **Recherche** : PostgreSQL full-text (`tsvector`), ne pas introduire de moteur externe.
- Code et identifiants en anglais ; commentaires/documentation produit en français.

## Workflow

Ce dépôt suit le flux superpowers : brainstorming → spec → plan d'implémentation →
exécution. Avant d'implémenter une fonctionnalité, consulter la spec et, le cas échéant,
le plan d'implémentation. Respecter le découpage en phases défini dans la spec.
