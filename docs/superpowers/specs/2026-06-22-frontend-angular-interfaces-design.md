# Frontend Angular — Interfaces Admin / Éditeur / Visiteur (conception)

**Date :** 2026-06-22
**Projet :** MAP — Plateforme de gestion et de diffusion de contenus vidéo
**Périmètre :** Application web Angular 17 (SPA) couvrant les trois espaces rôle
(Visiteur public, Éditeur, Administrateur), avec un design professionnel et fluide.
**Spec backend associée :** [2026-06-21-plateforme-streaming-interne-design.md](2026-06-21-plateforme-streaming-interne-design.md).

---

## 1. Objectif

Concevoir et structurer le frontend de la plateforme vidéo MAP : une SPA Angular 17
unique qui consomme l'API REST `/api/v1` (JWT), offre un visionnage HLS anonyme, et
expose trois espaces distincts selon le rôle. Le rendu doit projeter l'image d'une
**agence de presse nationale** : sobre, dense, éditorial, digne de confiance.

État de départ : le dossier `frontend/` est un squelette (un `package.json`, deux
composants viewer `HlsPlayerComponent` / `ViewerPageComponent`, des README vides). Il
n'y a ni `angular.json`, ni bootstrap, ni routing, ni design system. On part donc
quasiment de zéro côté UI, en conservant les deux composants viewer existants.

## 2. Décisions de conception (validées en brainstorming)

| Sujet | Décision |
|---|---|
| **Identité visuelle** | « Newsroom institutionnel » : bleu encre `#0f2742`, rouge MAP `#c1272d`, neutres clairs (`paper`/`muted`). Mode clair. Sobre, dense, éditorial. |
| **Fondation UI** | **Tailwind CSS** (tokens MAP) + composants **headless** (Angular CDK) pour l'accessibilité, sans look imposé d'une librairie. Évite le « look Material » générique. |
| **État / données** | **Angular Signals** + services HTTP **typés** par domaine. Pas de NgRx (surdimensionné). |
| **Internationalisation** | **Bilingue FR + AR** avec bascule **RTL** globale (`dir="rtl"`). Police arabe dédiée (Noto Naskh). Classes logiques (`ms-`/`me-`) plutôt que `ml`/`mr`. |
| **Structure** | **Trois espaces distincts** : Visiteur (barre haute, public), Éditeur (sidebar, back-office), Admin (sidebar, back-office). Pas de coquille partagée à menus filtrés. |
| **Comptes** | **Auto-inscription Visiteur**. Visionnage **anonyme** autorisé ; **auth requise** pour commenter / liker / partager. |
| **Composants Angular** | **Standalone** (pas de NgModules). Zones **lazy-loaded**. |

## 3. Architecture applicative

Une seule application Angular 17 standalone, découpée en zones chargées à la demande.

```
frontend/src/app/
  core/        # AuthStore (signals), JWT interceptor, guards, i18n, http, gestion d'erreur
  shared/      # design system : composants UI headless + stylés
  layouts/     # PublicShell · EditorShell · AdminShell (3 coquilles distinctes)
  features/
    public/    # catalogue, détail vidéo + lecteur, recherche, login / register
    editor/    # dashboard, mes vidéos, upload chunké, métadonnées, publish / archive
    admin/     # dashboard global, utilisateurs / rôles, stats, audit, config
  styles/      # tailwind.css + tokens (couleurs, typo, radius, ombres)
```

**Routing (lazy) :**

| Préfixe | Espace | Coquille | Accès |
|---|---|---|---|
| `/` | Visiteur (public) | PublicShell (barre haute) | Anonyme |
| `/auth/*` | Login / Register | (minimale) | Anonyme |
| `/studio/*` | Éditeur | EditorShell (sidebar) | Rôle Éditeur |
| `/admin/*` | Administrateur | AdminShell (sidebar) | Rôle Admin |

## 4. Design system (Tailwind + tokens)

- `tailwind.config.js` expose les **tokens MAP** : couleurs `ink` / `map-red` / `paper`
  / `muted`, échelle d'espacement, `rounded-lg`, ombres douces, polices (latine + arabe).
- Composants `shared/ui` : `Button`, `Card`, `DataTable` (tri / pagination via CDK),
  `Modal` / `Drawer` (CDK Overlay), `Toast`, `FormField` + validation, `StatusBadge`
  (cycle `Draft → Processing → Ready → Published → Archived` + `Failed`), `FileUploader`
  (upload chunké), `StatCard`, `EmptyState`, `Spinner`, `Pagination`, `SearchBar`.
- **RTL** : attribut `dir` piloté par la langue active ; utilisation systématique des
  propriétés logiques pour que les layouts se reflètent en arabe.

## 5. Auth, sécurité & flux

- **`AuthStore`** (signals) : `user`, `roles`, `token`, `isAuthenticated`. Login /
  register via `POST /api/v1/auth/login` et `POST /api/v1/auth/register`.
- **HTTP interceptor** : ajoute `Authorization: Bearer <token>` ; gère `401`
  (rafraîchissement ou déconnexion) ; mappe les réponses `ProblemDetails` en toasts
  globaux ou erreurs de champ de formulaire.
- **Guards** : `authGuard` (toute action authentifiée), `editorGuard` (rôle Éditeur),
  `adminGuard` (rôle Admin). L'espace public reste accessible sans authentification.
- Persistance du token : à trancher à l'implémentation (mémoire + refresh, ou stockage) —
  par défaut, token en mémoire avec rafraîchissement, selon ce que l'API exposera.

## 6. Périmètre fonctionnel par espace (aligné sur l'API `/api/v1`)

### Visiteur (public)
- Catalogue paginé des vidéos publiées + recherche `?q=` (`GET /videos`).
- Page vidéo : détail + lecteur HLS (`GET /videos/{id}`, `GET /videos/{id}/stream`).
- Lecteur hls.js existant **enrichi** : contrôles UI, sélection de qualité (renditions),
  télémétrie de visionnage (`POST /videos/{id}/views`).
- Si connecté : commenter / liker / partager (`POST /videos/{id}/comments|likes|shares`).
- Login + inscription (`/auth/*`).

### Éditeur (`/studio`)
- Tableau de bord (compteurs, file de transcodage, état des vidéos).
- *Mes vidéos* (`GET /videos/mine`).
- **Upload chunké** (`POST /videos`, `POST /videos/{id}/upload`) + suivi du transcodage
  (statuts, progression, erreurs).
- Édition des métadonnées (`PUT /videos/{id}`), `publish` / `archive`.
- Recherche / indexation.

### Administrateur (`/admin`)
- Tableau de bord global.
- **Utilisateurs & rôles** : CRUD + attribution de rôle
  (`GET/POST/PUT/DELETE /users`, `POST /users/{id}/roles`).
- **Statistiques** (`GET /stats`) : vues, temps de visionnage, top contenus.
- **Audit** (`GET /audit`).
- **Configuration système** (`GET/PUT /config`).

## 7. Lecteur vidéo

Réutilisation de `HlsPlayerComponent` et `ViewerPageComponent` existants. Ajouts :
contrôles UI complets, sélecteur de qualité (variantes de rendition), envoi de la
télémétrie de visionnage. Le lecteur reste intégrable ailleurs (piloté par l'API de
diffusion).

## 8. Tests & qualité

- **Jest** : `AuthStore` et services de domaine (signals), interceptor, guards, et
  composants critiques (uploader, table, formulaires).
- **Cypress** : parcours e2e par rôle — Éditeur (login → upload → publish), Visiteur
  (catalogue → visionnage → like), Admin (gestion d'un utilisateur).
- **Accessibilité** : gestion du focus via CDK, contrastes AA, navigation clavier,
  support RTL vérifié.

## 9. Scaffolding à créer

`angular.json`, `tsconfig*.json`, `main.ts` (`bootstrapApplication`), `app.config.ts`
(router, `provideHttpClient` + interceptors, i18n), `index.html`, configuration
Tailwind / PostCSS, configuration Jest + Cypress, et l'arborescence de la section 3.

## 10. État du backend & endpoints consommés

**Correction de cadrage (vérifiée le 2026-06-22 contre le code) :** contrairement à une
hypothèse initiale, le backend a traversé les **Phases 1 à 6** et est quasi complet. Les
endpoints suivants existent déjà et sont consommables immédiatement :

- **Auth** : `POST /auth/register`, `POST /auth/login`, `GET /auth/me`
  (JWT unique, sans refresh token ; re-login à l'expiration).
- **Catalogue/Vidéo** : `GET /videos` (q, categoryId, tag, page), `GET /videos/{id}`,
  `GET /videos/mine`, `PUT /videos/{id}`, `POST /videos/{id}/publish`·`/archive`.
- **Upload** : `POST /videos` (draft), `POST /videos/{id}/upload/chunk`, `/upload/complete`.
- **Diffusion** : `GET /videos/{id}/stream`, `GET /videos/{id}/hls/{**path}` (proxy MinIO).
- **Engagement** : commentaires (GET/POST/DELETE), likes (POST/DELETE), shares,
  `GET /videos/{id}/engagement`, `POST /videos/{id}/views`.
- **Admin** : `GET /stats`, `GET /audit`, `GET/PUT /config`.
- **Users** : `GET /users`, `PUT /users/{id}`, `POST /users/{id}/roles`,
  `DELETE /users/{id}/roles/{role}`.

**Seuls trous identifiés**, à combler avant le frontend (pas de mocks) :
`GET /categories` + CRUD Admin des catégories, et `GET /tags` (autocomplétion). Ces
endpoints sont requis par le filtre du catalogue et les formulaires éditeur/admin.

### Ordre de livraison du frontend

1. **Socle + design system + espace public** : scaffolding, Tailwind/tokens, coquille
   publique, catalogue + filtres (catégories/tags) + page vidéo + lecteur enrichi.
2. **Auth & espace Éditeur** : login/register, guards, interceptor, dashboard éditeur,
   upload chunké + suivi transcodage, métadonnées, publish/archive.
3. **Espace Admin** : utilisateurs/rôles, statistiques, audit, configuration, catégories.
4. **i18n FR/AR + RTL** transversal et finitions d'accessibilité.

Chaque phase reste livrable indépendamment ; l'API étant déjà en place, le frontend se
développe contre des **contrats typés réels** (pas de mocks).

## 11. Hors périmètre

- Application mobile native.
- Thème sombre (envisageable ultérieurement pour le viewer ; non requis ici).
- Moteur de recherche externe (recherche = PostgreSQL full-text côté API).
- Refresh token (le backend utilise un JWT unique ; à introduire côté API si besoin).

## 12. Pré-requis backend à implémenter (avant Phase 1 frontend)

Endpoints à ajouter, en suivant les conventions existantes (service scoped + controller
mince + `ProblemDetails` + tests HTTP) :

- `GET /api/v1/categories` — liste publique des catégories (Id, Name, Slug).
- `POST /api/v1/categories` — création (Admin) ; slug dérivé du nom.
- `PUT /api/v1/categories/{id}` — renommage (Admin).
- `DELETE /api/v1/categories/{id}` — suppression (Admin) ; refus si des vidéos y sont
  rattachées (ou détachement selon règle retenue).
- `GET /api/v1/tags` — liste/autocomplétion des tags (filtre `?q=` optionnel).
