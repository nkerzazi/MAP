# Frontend MAP — Phase 1 : Socle + Design System + Espace Public — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Mettre en place le socle Angular 17 (scaffolding, design system Tailwind « Newsroom », couche HTTP typée) et livrer un espace public navigable : catalogue avec recherche/filtres, page de détail, et lecteur HLS enrichi avec télémétrie.

**Architecture :** Application Angular 17 **standalone** (pas de NgModules), zones lazy-loaded. État via **Signals** + services HTTP typés par domaine. Style via **Tailwind CSS** (tokens MAP) + composants headless **Angular CDK**. Cette Phase 1 ne couvre que l'espace public anonyme ; auth/éditeur/admin/i18n font l'objet de plans séparés.

**Tech Stack :** Angular 17, TypeScript 5.4, Tailwind CSS 3, @angular/cdk, hls.js, RxJS, Jest (jest-preset-angular), Cypress.

**Spec de référence :** [docs/superpowers/specs/2026-06-22-frontend-angular-interfaces-design.md](../specs/2026-06-22-frontend-angular-interfaces-design.md). API consommée : `GET /api/v1/videos`, `GET /api/v1/videos/{id}`, `GET /api/v1/videos/{id}/stream`, `POST /api/v1/videos/{id}/views`, `GET /api/v1/categories`, `GET /api/v1/tags` (tous publics/anonymes).

**Pré-requis :** Node 20+, npm. Le backend tourne sur `http://localhost:5080` (ajuster le proxy si besoin). Tous les chemins sont relatifs à `frontend/`.

---

## Structure de fichiers (Phase 1)

```
frontend/
  angular.json · tsconfig*.json · jest.config.js · setup-jest.ts · proxy.conf.json
  tailwind.config.js · postcss.config.js
  src/
    main.ts · index.html · styles.css
    environments/environment.ts · environment.prod.ts
    app/
      app.config.ts · app.routes.ts · app.component.ts
      core/
        models/video.models.ts        # types DTO alignés sur l'API
        api/catalog.service.ts         # GET /videos, /videos/{id}
        api/category.service.ts        # GET /categories
        api/tag.service.ts             # GET /tags
        api/analytics.service.ts       # POST /videos/{id}/views
      shared/ui/
        button.component.ts · status-badge.component.ts · spinner.component.ts
        pagination.component.ts · search-bar.component.ts · video-card.component.ts
      layouts/public-shell.component.ts
      features/public/
        catalog-page.component.ts
        video-detail-page.component.ts
        hls-player.component.ts        # déplacé depuis viewer/, enrichi
```

**Note :** les composants existants `src/app/viewer/hls-player.component.ts` et `viewer-page.component.ts` sont remplacés par les versions enrichies de ce plan (Task 9 / Task 10). Les supprimer dans la Task concernée.

---

## Task 1 : Scaffolding Angular + outils

**Files:**
- Modify: `frontend/package.json`
- Create: `frontend/angular.json`, `frontend/tsconfig.json`, `frontend/tsconfig.app.json`, `frontend/tsconfig.spec.json`
- Create: `frontend/src/main.ts`, `frontend/src/index.html`, `frontend/src/styles.css`
- Create: `frontend/src/environments/environment.ts`, `frontend/src/environments/environment.prod.ts`
- Create: `frontend/proxy.conf.json`

- [ ] **Step 1 : Remplacer `package.json`**

```json
{
  "name": "map-media-platform-frontend",
  "version": "0.1.0",
  "description": "Interface Angular 17 (admin / éditeur / viewer) de la plateforme vidéo MAP",
  "scripts": {
    "start": "ng serve --proxy-config proxy.conf.json",
    "build": "ng build",
    "test": "jest",
    "e2e": "cypress run"
  },
  "dependencies": {
    "@angular/animations": "^17.3.0",
    "@angular/cdk": "^17.3.0",
    "@angular/common": "^17.3.0",
    "@angular/compiler": "^17.3.0",
    "@angular/core": "^17.3.0",
    "@angular/forms": "^17.3.0",
    "@angular/platform-browser": "^17.3.0",
    "@angular/platform-browser-dynamic": "^17.3.0",
    "@angular/router": "^17.3.0",
    "hls.js": "^1.5.0",
    "rxjs": "^7.8.0",
    "tslib": "^2.6.0",
    "zone.js": "~0.14.4"
  },
  "devDependencies": {
    "@angular-devkit/build-angular": "^17.3.0",
    "@angular/cli": "^17.3.0",
    "@angular/compiler-cli": "^17.3.0",
    "@types/jest": "^29.5.0",
    "autoprefixer": "^10.4.0",
    "cypress": "^13.0.0",
    "jest": "^29.7.0",
    "jest-preset-angular": "^14.1.0",
    "postcss": "^8.4.0",
    "tailwindcss": "^3.4.0",
    "typescript": "~5.4.0"
  }
}
```

- [ ] **Step 2 : Créer `tsconfig.json`**

```json
{
  "compileOnSave": false,
  "compilerOptions": {
    "outDir": "./dist/out-tsc",
    "strict": true,
    "noImplicitOverride": true,
    "noPropertyAccessFromIndexSignature": true,
    "noImplicitReturns": true,
    "noFallthroughCasesInSwitch": true,
    "skipLibCheck": true,
    "esModuleInterop": true,
    "sourceMap": true,
    "declaration": false,
    "experimentalDecorators": true,
    "moduleResolution": "bundler",
    "importHelpers": true,
    "target": "ES2022",
    "module": "ES2022",
    "lib": ["ES2022", "dom"]
  },
  "angularCompilerOptions": {
    "enableI18nLegacyMessageIdFormat": false,
    "strictInjectionParameters": true,
    "strictInputAccessModifiers": true,
    "strictTemplates": true
  }
}
```

- [ ] **Step 3 : Créer `tsconfig.app.json`**

```json
{
  "extends": "./tsconfig.json",
  "compilerOptions": { "outDir": "./out-tsc/app", "types": [] },
  "files": ["src/main.ts"],
  "include": ["src/**/*.d.ts"]
}
```

- [ ] **Step 4 : Créer `tsconfig.spec.json`**

```json
{
  "extends": "./tsconfig.json",
  "compilerOptions": { "outDir": "./out-tsc/spec", "types": ["jest", "node"] },
  "include": ["src/**/*.spec.ts", "src/**/*.d.ts"]
}
```

- [ ] **Step 5 : Créer `angular.json`**

```json
{
  "$schema": "./node_modules/@angular/cli/lib/config/schema.json",
  "version": 1,
  "newProjectRoot": "projects",
  "projects": {
    "map-frontend": {
      "projectType": "application",
      "root": "",
      "sourceRoot": "src",
      "prefix": "app",
      "architect": {
        "build": {
          "builder": "@angular-devkit/build-angular:application",
          "options": {
            "outputPath": "dist/map-frontend",
            "index": "src/index.html",
            "browser": "src/main.ts",
            "polyfills": ["zone.js"],
            "tsConfig": "tsconfig.app.json",
            "assets": ["src/favicon.ico", "src/assets"],
            "styles": ["src/styles.css"],
            "scripts": []
          },
          "configurations": {
            "production": {
              "fileReplacements": [
                { "replace": "src/environments/environment.ts", "with": "src/environments/environment.prod.ts" }
              ],
              "optimization": true,
              "outputHashing": "all"
            },
            "development": { "optimization": false, "sourceMap": true }
          },
          "defaultConfiguration": "production"
        },
        "serve": {
          "builder": "@angular-devkit/build-angular:dev-server",
          "configurations": {
            "production": { "buildTarget": "map-frontend:build:production" },
            "development": { "buildTarget": "map-frontend:build:development" }
          },
          "defaultConfiguration": "development"
        }
      }
    }
  }
}
```

- [ ] **Step 6 : Créer `src/index.html`**

```html
<!doctype html>
<html lang="fr">
<head>
  <meta charset="utf-8" />
  <title>MAP Vidéo</title>
  <base href="/" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <link rel="icon" type="image/x-icon" href="favicon.ico" />
</head>
<body>
  <app-root></app-root>
</body>
</html>
```

- [ ] **Step 7 : Créer `src/main.ts`**

```typescript
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

bootstrapApplication(AppComponent, appConfig).catch((err) => console.error(err));
```

- [ ] **Step 8 : Créer `src/styles.css`** (les directives Tailwind ; complété en Task 2)

```css
@tailwind base;
@tailwind components;
@tailwind utilities;

html, body { height: 100%; }
body { @apply bg-paper text-ink font-sans antialiased; margin: 0; }
```

- [ ] **Step 9 : Créer `src/environments/environment.ts` et `environment.prod.ts`**

`environment.ts` :
```typescript
export const environment = { production: false, apiBase: '/api/v1' };
```
`environment.prod.ts` :
```typescript
export const environment = { production: true, apiBase: '/api/v1' };
```

- [ ] **Step 10 : Créer `proxy.conf.json`** (redirige `/api` vers le backend en dev)

```json
{ "/api": { "target": "http://localhost:5080", "secure": false, "changeOrigin": true } }
```

- [ ] **Step 11 : Installer et vérifier le build**

Run: `cd frontend && npm install && npm run build`
Expected: build échoue tant que `app.config.ts` / `app.component.ts` n'existent pas — c'est attendu, ils sont créés en Task 3 et Task 5. Vérifier seulement que `npm install` réussit sans erreur de résolution.

- [ ] **Step 12 : Commit**

```bash
git add frontend/package.json frontend/angular.json frontend/tsconfig*.json frontend/proxy.conf.json frontend/src/main.ts frontend/src/index.html frontend/src/styles.css frontend/src/environments
git commit -m "chore(frontend): scaffolding Angular 17 standalone (build, tsconfig, env, proxy)"
```

---

## Task 2 : Design system — tokens Tailwind « Newsroom »

**Files:**
- Create: `frontend/tailwind.config.js`, `frontend/postcss.config.js`

- [ ] **Step 1 : Créer `tailwind.config.js`** (palette MAP : bleu encre + rouge MAP)

```javascript
/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./src/**/*.{html,ts}'],
  theme: {
    extend: {
      colors: {
        ink: { DEFAULT: '#0f2742', 600: '#13355c', 500: '#1c3a5c', 300: '#a9bcd4' },
        'map-red': { DEFAULT: '#c1272d', 600: '#a81f25' },
        paper: '#f4f5f7',
        muted: '#8a93a3',
        line: '#e3e6ea'
      },
      fontFamily: {
        sans: ['"Inter"', 'system-ui', 'sans-serif'],
        arabic: ['"Noto Naskh Arabic"', 'serif']
      },
      borderRadius: { lg: '0.75rem' },
      boxShadow: { soft: '0 1px 4px rgba(20,40,80,.08)' }
    }
  },
  plugins: []
};
```

- [ ] **Step 2 : Créer `postcss.config.js`**

```javascript
module.exports = { plugins: { tailwindcss: {}, autoprefixer: {} } };
```

- [ ] **Step 3 : Vérifier la compilation Tailwind**

Run: `cd frontend && npx tailwindcss -i src/styles.css -o /tmp/out.css --config tailwind.config.js`
Expected: génère `/tmp/out.css` sans erreur (les classes `bg-paper`, `text-ink` sont reconnues).

- [ ] **Step 4 : Commit**

```bash
git add frontend/tailwind.config.js frontend/postcss.config.js
git commit -m "feat(frontend): design system - tokens Tailwind Newsroom (ink + map-red)"
```

---

## Task 3 : Configuration Jest + app bootstrap minimal

**Files:**
- Create: `frontend/jest.config.js`, `frontend/setup-jest.ts`
- Create: `frontend/src/app/app.config.ts`, `frontend/src/app/app.routes.ts`, `frontend/src/app/app.component.ts`
- Test: `frontend/src/app/app.component.spec.ts`

- [ ] **Step 1 : Créer `jest.config.js`**

```javascript
module.exports = {
  preset: 'jest-preset-angular',
  setupFilesAfterEnv: ['<rootDir>/setup-jest.ts'],
  testPathIgnorePatterns: ['<rootDir>/node_modules/', '<rootDir>/dist/', '<rootDir>/cypress/'],
  moduleNameMapper: { '^src/(.*)$': '<rootDir>/src/$1' }
};
```

- [ ] **Step 2 : Créer `setup-jest.ts`**

```typescript
import 'jest-preset-angular/setup-jest';
```

- [ ] **Step 3 : Créer `src/app/app.routes.ts`** (routes lazy ; seul le public est branché en Phase 1)

```typescript
import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./layouts/public-shell.component').then((m) => m.PublicShellComponent),
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/public/catalog-page.component').then((m) => m.CatalogPageComponent)
      },
      {
        path: 'videos/:id',
        loadComponent: () =>
          import('./features/public/video-detail-page.component').then((m) => m.VideoDetailPageComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
```

- [ ] **Step 4 : Créer `src/app/app.config.ts`**

```typescript
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withFetch())
  ]
};
```

- [ ] **Step 5 : Écrire le test qui échoue `src/app/app.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  it('crée le composant racine', async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [provideRouter([])]
    }).compileComponents();
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
```

- [ ] **Step 6 : Lancer le test → échec attendu**

Run: `cd frontend && npx jest src/app/app.component.spec.ts`
Expected: FAIL — `Cannot find module './app.component'`.

- [ ] **Step 7 : Créer `src/app/app.component.ts`**

```typescript
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet></router-outlet>`
})
export class AppComponent {}
```

- [ ] **Step 8 : Lancer le test → succès**

Run: `cd frontend && npx jest src/app/app.component.spec.ts`
Expected: PASS.

- [ ] **Step 9 : Commit**

```bash
git add frontend/jest.config.js frontend/setup-jest.ts frontend/src/app/app.config.ts frontend/src/app/app.routes.ts frontend/src/app/app.component.ts frontend/src/app/app.component.spec.ts
git commit -m "feat(frontend): bootstrap app standalone + routing lazy + config Jest"
```

---

## Task 4 : Modèles DTO + CatalogService

**Files:**
- Create: `frontend/src/app/core/models/video.models.ts`
- Create: `frontend/src/app/core/api/catalog.service.ts`
- Test: `frontend/src/app/core/api/catalog.service.spec.ts`

- [ ] **Step 1 : Créer les modèles `src/app/core/models/video.models.ts`** (alignés sur l'API backend)

```typescript
export interface VideoListItem {
  id: string;
  title: string;
  slug: string;
  categoryName: string | null;
  durationSeconds: number | null;
  publishedAt: string | null;
  status: string;
}

export interface RenditionInfo {
  resolution: string;
  bitrate: number;
  manifestKey: string;
}

export interface VideoDetail {
  id: string;
  title: string;
  description: string | null;
  slug: string;
  status: string;
  categoryName: string | null;
  durationSeconds: number | null;
  publishedAt: string | null;
  tags: string[];
  renditions: RenditionInfo[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface Category { id: string; name: string; slug: string; }
export interface Tag { id: string; name: string; }

export interface CatalogFilters {
  q?: string;
  categoryId?: string;
  tag?: string;
  page?: number;
}
```

- [ ] **Step 2 : Écrire le test qui échoue `src/app/core/api/catalog.service.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { CatalogService } from './catalog.service';

describe('CatalogService', () => {
  let service: CatalogService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CatalogService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CatalogService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('construit la requête catalogue avec q, categoryId, tag et page', () => {
    service.list({ q: 'sommet', categoryId: 'c1', tag: 'sport', page: 2 }).subscribe();
    const req = http.expectOne(
      '/api/v1/videos?q=sommet&categoryId=c1&tag=sport&page=2'
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], total: 0, page: 2, pageSize: 20 });
  });

  it('récupère le détail d’une vidéo', () => {
    service.get('v1').subscribe();
    const req = http.expectOne('/api/v1/videos/v1');
    expect(req.request.method).toBe('GET');
    req.flush({});
  });
});
```

- [ ] **Step 3 : Lancer le test → échec attendu**

Run: `cd frontend && npx jest catalog.service`
Expected: FAIL — `Cannot find module './catalog.service'`.

- [ ] **Step 4 : Créer `src/app/core/api/catalog.service.ts`**

```typescript
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { CatalogFilters, PagedResult, VideoDetail, VideoListItem } from '../models/video.models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/videos`;

  list(filters: CatalogFilters = {}): Observable<PagedResult<VideoListItem>> {
    let params = new HttpParams();
    if (filters.q) params = params.set('q', filters.q);
    if (filters.categoryId) params = params.set('categoryId', filters.categoryId);
    if (filters.tag) params = params.set('tag', filters.tag);
    params = params.set('page', String(filters.page ?? 1));
    return this.http.get<PagedResult<VideoListItem>>(this.base, { params });
  }

  get(id: string): Observable<VideoDetail> {
    return this.http.get<VideoDetail>(`${this.base}/${id}`);
  }
}
```

- [ ] **Step 5 : Lancer le test → succès**

Run: `cd frontend && npx jest catalog.service`
Expected: PASS (2 tests).

- [ ] **Step 6 : Commit**

```bash
git add frontend/src/app/core/models/video.models.ts frontend/src/app/core/api/catalog.service.ts frontend/src/app/core/api/catalog.service.spec.ts
git commit -m "feat(frontend): modèles DTO + CatalogService (list/get) avec tests"
```

---

## Task 5 : CategoryService, TagService, AnalyticsService

**Files:**
- Create: `frontend/src/app/core/api/category.service.ts`, `tag.service.ts`, `analytics.service.ts`
- Test: `frontend/src/app/core/api/reference.service.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `src/app/core/api/reference.service.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { CategoryService } from './category.service';
import { TagService } from './tag.service';
import { AnalyticsService } from './analytics.service';

describe('Services de référence', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('CategoryService liste les catégories', () => {
    TestBed.inject(CategoryService).list().subscribe();
    expect(http.expectOne('/api/v1/categories').request.method).toBe('GET');
  });

  it('TagService liste les tags avec filtre q', () => {
    TestBed.inject(TagService).list('spo').subscribe();
    expect(http.expectOne('/api/v1/tags?q=spo').request.method).toBe('GET');
  });

  it('AnalyticsService envoie la télémétrie de visionnage', () => {
    TestBed.inject(AnalyticsService).recordView('v1', { watchSeconds: 12, sessionId: 's1' }).subscribe();
    const req = http.expectOne('/api/v1/videos/v1/views');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ watchSeconds: 12, sessionId: 's1' });
    req.flush(null);
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `cd frontend && npx jest reference.service`
Expected: FAIL — modules introuvables.

- [ ] **Step 3 : Créer `src/app/core/api/category.service.ts`**

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { Category } from '../models/video.models';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private http = inject(HttpClient);
  list(): Observable<Category[]> {
    return this.http.get<Category[]>(`${environment.apiBase}/categories`);
  }
}
```

- [ ] **Step 4 : Créer `src/app/core/api/tag.service.ts`**

```typescript
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { Tag } from '../models/video.models';

@Injectable({ providedIn: 'root' })
export class TagService {
  private http = inject(HttpClient);
  list(q?: string): Observable<Tag[]> {
    let params = new HttpParams();
    if (q) params = params.set('q', q);
    return this.http.get<Tag[]>(`${environment.apiBase}/tags`, { params });
  }
}
```

- [ ] **Step 5 : Créer `src/app/core/api/analytics.service.ts`**

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';

export interface RecordViewBody { watchSeconds: number; sessionId: string; }

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private http = inject(HttpClient);
  recordView(videoId: string, body: RecordViewBody): Observable<void> {
    return this.http.post<void>(`${environment.apiBase}/videos/${videoId}/views`, body);
  }
}
```

- [ ] **Step 6 : Lancer → succès**

Run: `cd frontend && npx jest reference.service`
Expected: PASS (3 tests).

- [ ] **Step 7 : Commit**

```bash
git add frontend/src/app/core/api/category.service.ts frontend/src/app/core/api/tag.service.ts frontend/src/app/core/api/analytics.service.ts frontend/src/app/core/api/reference.service.spec.ts
git commit -m "feat(frontend): CategoryService, TagService, AnalyticsService avec tests"
```

---

## Task 6 : Composants UI partagés (StatusBadge, Spinner, SearchBar)

**Files:**
- Create: `frontend/src/app/shared/ui/status-badge.component.ts`, `spinner.component.ts`, `search-bar.component.ts`
- Test: `frontend/src/app/shared/ui/status-badge.component.spec.ts`, `search-bar.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `status-badge.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { StatusBadgeComponent } from './status-badge.component';

describe('StatusBadgeComponent', () => {
  it('affiche le libellé français du statut', async () => {
    await TestBed.configureTestingModule({ imports: [StatusBadgeComponent] }).compileComponents();
    const fixture = TestBed.createComponent(StatusBadgeComponent);
    fixture.componentRef.setInput('status', 'Published');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Publiée');
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `cd frontend && npx jest status-badge`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/shared/ui/status-badge.component.ts`**

```typescript
import { Component, Input, computed, signal } from '@angular/core';

const LABELS: Record<string, { fr: string; cls: string }> = {
  Draft: { fr: 'Brouillon', cls: 'bg-line text-ink' },
  Processing: { fr: 'En traitement', cls: 'bg-amber-100 text-amber-800' },
  Ready: { fr: 'Prête', cls: 'bg-sky-100 text-sky-800' },
  Published: { fr: 'Publiée', cls: 'bg-emerald-100 text-emerald-800' },
  Archived: { fr: 'Archivée', cls: 'bg-line text-muted' },
  Failed: { fr: 'Échec', cls: 'bg-red-100 text-map-red' }
};

@Component({
  selector: 'app-status-badge',
  standalone: true,
  template: `<span class="inline-block px-2 py-0.5 rounded-full text-xs font-semibold" [class]="view().cls">{{ view().fr }}</span>`
})
export class StatusBadgeComponent {
  private _status = signal('Draft');
  @Input({ required: true }) set status(v: string) { this._status.set(v); }
  view = computed(() => LABELS[this._status()] ?? { fr: this._status(), cls: 'bg-line text-ink' });
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `cd frontend && npx jest status-badge`
Expected: PASS.

- [ ] **Step 5 : Créer `src/app/shared/ui/spinner.component.ts`** (pas de test — purement visuel)

```typescript
import { Component } from '@angular/core';

@Component({
  selector: 'app-spinner',
  standalone: true,
  template: `<div class="inline-block w-6 h-6 border-2 border-line border-t-ink rounded-full animate-spin" role="status" aria-label="Chargement"></div>`
})
export class SpinnerComponent {}
```

- [ ] **Step 6 : Écrire le test qui échoue `search-bar.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { SearchBarComponent } from './search-bar.component';

describe('SearchBarComponent', () => {
  it('émet le terme recherché à la soumission', async () => {
    await TestBed.configureTestingModule({ imports: [SearchBarComponent] }).compileComponents();
    const fixture = TestBed.createComponent(SearchBarComponent);
    const cmp = fixture.componentInstance;
    let emitted = '';
    cmp.search.subscribe((v: string) => (emitted = v));
    fixture.detectChanges();
    cmp.value = 'sommet';
    cmp.submit();
    expect(emitted).toBe('sommet');
  });
});
```

- [ ] **Step 7 : Lancer → échec attendu**

Run: `cd frontend && npx jest search-bar`
Expected: FAIL — module introuvable.

- [ ] **Step 8 : Créer `src/app/shared/ui/search-bar.component.ts`**

```typescript
import { Component, EventEmitter, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-search-bar',
  standalone: true,
  imports: [FormsModule],
  template: `
    <form (ngSubmit)="submit()" class="flex items-center gap-2">
      <input [(ngModel)]="value" name="q" type="search"
             placeholder="Rechercher une vidéo…"
             class="flex-1 px-3 py-2 rounded-lg border border-line bg-white focus:outline-none focus:ring-2 focus:ring-ink/30" />
      <button type="submit" class="px-3 py-2 rounded-lg bg-ink text-white text-sm font-semibold">Rechercher</button>
    </form>
  `
})
export class SearchBarComponent {
  value = '';
  @Output() search = new EventEmitter<string>();
  submit() { this.search.emit(this.value.trim()); }
}
```

- [ ] **Step 9 : Lancer → succès**

Run: `cd frontend && npx jest search-bar`
Expected: PASS.

- [ ] **Step 10 : Commit**

```bash
git add frontend/src/app/shared/ui
git commit -m "feat(frontend): composants UI partagés (status-badge, spinner, search-bar) avec tests"
```

---

## Task 7 : VideoCardComponent + PaginationComponent

**Files:**
- Create: `frontend/src/app/shared/ui/video-card.component.ts`, `pagination.component.ts`
- Test: `frontend/src/app/shared/ui/pagination.component.spec.ts`

- [ ] **Step 1 : Créer `src/app/shared/ui/video-card.component.ts`** (vignette catalogue ; visuel, pas de test)

```typescript
import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';
import { VideoListItem } from '../../core/models/video.models';

@Component({
  selector: 'app-video-card',
  standalone: true,
  imports: [RouterLink, DatePipe],
  template: `
    <a [routerLink]="['/videos', video.id]"
       class="block rounded-lg overflow-hidden bg-white border border-line shadow-soft hover:shadow-md transition-shadow">
      <div class="aspect-video bg-gradient-to-br from-ink-600 to-ink-500"></div>
      <div class="p-3">
        <h3 class="font-semibold text-ink line-clamp-2">{{ video.title }}</h3>
        <p class="mt-1 text-xs text-muted">
          {{ video.categoryName }}
          <span *ngIf="video.publishedAt">· {{ video.publishedAt | date: 'mediumDate' }}</span>
        </p>
      </div>
    </a>
  `
})
export class VideoCardComponent {
  @Input({ required: true }) video!: VideoListItem;
}
```

- [ ] **Step 2 : Écrire le test qui échoue `pagination.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { PaginationComponent } from './pagination.component';

describe('PaginationComponent', () => {
  it('calcule le nombre de pages et émet la navigation', async () => {
    await TestBed.configureTestingModule({ imports: [PaginationComponent] }).compileComponents();
    const fixture = TestBed.createComponent(PaginationComponent);
    const cmp = fixture.componentInstance;
    fixture.componentRef.setInput('total', 45);
    fixture.componentRef.setInput('pageSize', 20);
    fixture.componentRef.setInput('page', 1);
    fixture.detectChanges();
    expect(cmp.totalPages()).toBe(3);
    let target = 0;
    cmp.pageChange.subscribe((p: number) => (target = p));
    cmp.go(2);
    expect(target).toBe(2);
  });
});
```

- [ ] **Step 3 : Lancer → échec attendu**

Run: `cd frontend && npx jest pagination`
Expected: FAIL — module introuvable.

- [ ] **Step 4 : Créer `src/app/shared/ui/pagination.component.ts`**

```typescript
import { Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';

@Component({
  selector: 'app-pagination',
  standalone: true,
  template: `
    <nav class="flex items-center justify-center gap-2" *ngIf="totalPages() > 1">
      <button (click)="go(page() - 1)" [disabled]="page() === 1"
              class="px-3 py-1.5 rounded-lg border border-line disabled:opacity-40">Précédent</button>
      <span class="text-sm text-muted">Page {{ page() }} / {{ totalPages() }}</span>
      <button (click)="go(page() + 1)" [disabled]="page() === totalPages()"
              class="px-3 py-1.5 rounded-lg border border-line disabled:opacity-40">Suivant</button>
    </nav>
  `
})
export class PaginationComponent {
  private _total = signal(0);
  private _size = signal(20);
  private _page = signal(1);
  @Input({ required: true }) set total(v: number) { this._total.set(v); }
  @Input() set pageSize(v: number) { this._size.set(v || 20); }
  @Input({ required: true }) set page(v: number) { this._page.set(v); }
  @Output() pageChange = new EventEmitter<number>();

  page = this._page.asReadonly();
  totalPages = computed(() => Math.max(1, Math.ceil(this._total() / this._size())));

  go(p: number) {
    if (p < 1 || p > this.totalPages()) return;
    this.pageChange.emit(p);
  }
}
```

Note : le getter `page` est exposé en lecture seule pour le template ; l'`@Input set page` alimente le signal sous-jacent.

- [ ] **Step 5 : Lancer → succès**

Run: `cd frontend && npx jest pagination`
Expected: PASS.

- [ ] **Step 6 : Commit**

```bash
git add frontend/src/app/shared/ui/video-card.component.ts frontend/src/app/shared/ui/pagination.component.ts frontend/src/app/shared/ui/pagination.component.spec.ts
git commit -m "feat(frontend): VideoCard + Pagination (signals) avec test"
```

---

## Task 8 : PublicShellComponent (coquille publique)

**Files:**
- Create: `frontend/src/app/layouts/public-shell.component.ts`
- Test: `frontend/src/app/layouts/public-shell.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `public-shell.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PublicShellComponent } from './public-shell.component';

describe('PublicShellComponent', () => {
  it('affiche la barre de marque MAP et un router-outlet', async () => {
    await TestBed.configureTestingModule({
      imports: [PublicShellComponent],
      providers: [provideRouter([])]
    }).compileComponents();
    const fixture = TestBed.createComponent(PublicShellComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('MAP');
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `cd frontend && npx jest public-shell`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/layouts/public-shell.component.ts`**

```typescript
import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-public-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink],
  template: `
    <div class="min-h-screen flex flex-col bg-paper">
      <header class="h-14 bg-ink text-white flex items-center gap-4 px-4">
        <a routerLink="/" class="flex items-center gap-2 font-extrabold">
          <span class="w-2.5 h-2.5 bg-map-red rounded-sm"></span> MAP&nbsp;Vidéo
        </a>
        <div class="ms-auto flex items-center gap-3 text-sm">
          <span class="bg-ink-500 rounded-md px-2 py-1 font-semibold">FR · ع</span>
          <a class="bg-map-red rounded-lg px-3 py-1.5 font-semibold">Connexion</a>
        </div>
      </header>
      <main class="flex-1 max-w-6xl w-full mx-auto px-4 py-6">
        <router-outlet></router-outlet>
      </main>
      <footer class="text-center text-xs text-muted py-4">Maghreb Arabe Presse — plateforme vidéo interne</footer>
    </div>
  `
})
export class PublicShellComponent {}
```

Note : la bascule `FR · ع` et le lien Connexion sont des éléments inertes en Phase 1 (i18n et auth dans des plans ultérieurs).

- [ ] **Step 4 : Lancer → succès**

Run: `cd frontend && npx jest public-shell`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add frontend/src/app/layouts
git commit -m "feat(frontend): coquille publique (barre MAP, classes logiques RTL-ready)"
```

---

## Task 9 : CatalogPageComponent (catalogue + recherche + filtres)

**Files:**
- Create: `frontend/src/app/features/public/catalog-page.component.ts`
- Test: `frontend/src/app/features/public/catalog-page.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `catalog-page.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CatalogPageComponent } from './catalog-page.component';

describe('CatalogPageComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CatalogPageComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge catalogue + catégories au démarrage et affiche les vignettes', () => {
    const fixture = TestBed.createComponent(CatalogPageComponent);
    fixture.detectChanges();

    http.expectOne('/api/v1/categories').flush([{ id: 'c1', name: 'Actualités', slug: 'actualites' }]);
    http.expectOne((r) => r.url === '/api/v1/videos').flush({
      items: [{ id: 'v1', title: 'Sommet', slug: 'sommet', categoryName: 'Actualités', durationSeconds: null, publishedAt: null, status: 'Published' }],
      total: 1, page: 1, pageSize: 20
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sommet');
  });

  it('relance la recherche quand un terme est soumis', () => {
    const fixture = TestBed.createComponent(CatalogPageComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/categories').flush([]);
    http.expectOne((r) => r.url === '/api/v1/videos').flush({ items: [], total: 0, page: 1, pageSize: 20 });

    fixture.componentInstance.onSearch('sommet');
    const req = http.expectOne((r) => r.url === '/api/v1/videos' && r.params.get('q') === 'sommet');
    expect(req.request.params.get('q')).toBe('sommet');
    req.flush({ items: [], total: 0, page: 1, pageSize: 20 });
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `cd frontend && npx jest catalog-page`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/public/catalog-page.component.ts`**

```typescript
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CatalogService } from '../../core/api/catalog.service';
import { CategoryService } from '../../core/api/category.service';
import { Category, VideoListItem } from '../../core/models/video.models';
import { VideoCardComponent } from '../../shared/ui/video-card.component';
import { SearchBarComponent } from '../../shared/ui/search-bar.component';
import { PaginationComponent } from '../../shared/ui/pagination.component';
import { SpinnerComponent } from '../../shared/ui/spinner.component';

@Component({
  selector: 'app-catalog-page',
  standalone: true,
  imports: [CommonModule, VideoCardComponent, SearchBarComponent, PaginationComponent, SpinnerComponent],
  template: `
    <section class="space-y-5">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-center">
        <h1 class="text-xl font-bold text-ink">Catalogue</h1>
        <div class="sm:ms-auto sm:w-80"><app-search-bar (search)="onSearch($event)"></app-search-bar></div>
      </div>

      <div class="flex flex-wrap gap-2">
        <button (click)="filterCategory(null)"
                class="px-3 py-1 rounded-full text-sm border"
                [class]="!categoryId() ? 'bg-ink text-white border-ink' : 'border-line text-ink'">Toutes</button>
        <button *ngFor="let c of categories()" (click)="filterCategory(c.id)"
                class="px-3 py-1 rounded-full text-sm border"
                [class]="categoryId() === c.id ? 'bg-ink text-white border-ink' : 'border-line text-ink'">{{ c.name }}</button>
      </div>

      <div *ngIf="loading()" class="flex justify-center py-10"><app-spinner></app-spinner></div>

      <div *ngIf="!loading()">
        <p *ngIf="items().length === 0" class="text-muted py-10 text-center">Aucune vidéo trouvée.</p>
        <div class="grid grid-cols-2 md:grid-cols-3 gap-4">
          <app-video-card *ngFor="let v of items()" [video]="v"></app-video-card>
        </div>
        <div class="mt-6">
          <app-pagination [total]="total()" [pageSize]="pageSize()" [page]="page()" (pageChange)="goToPage($event)"></app-pagination>
        </div>
      </div>
    </section>
  `
})
export class CatalogPageComponent implements OnInit {
  private catalog = inject(CatalogService);
  private categoryApi = inject(CategoryService);

  items = signal<VideoListItem[]>([]);
  categories = signal<Category[]>([]);
  total = signal(0);
  page = signal(1);
  pageSize = signal(20);
  loading = signal(false);
  q = signal('');
  categoryId = signal<string | null>(null);

  ngOnInit(): void {
    this.categoryApi.list().subscribe((cs) => this.categories.set(cs));
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.catalog
      .list({ q: this.q() || undefined, categoryId: this.categoryId() ?? undefined, page: this.page() })
      .subscribe((res) => {
        this.items.set(res.items);
        this.total.set(res.total);
        this.pageSize.set(res.pageSize);
        this.loading.set(false);
      });
  }

  onSearch(term: string): void { this.q.set(term); this.page.set(1); this.load(); }
  filterCategory(id: string | null): void { this.categoryId.set(id); this.page.set(1); this.load(); }
  goToPage(p: number): void { this.page.set(p); this.load(); }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `cd frontend && npx jest catalog-page`
Expected: PASS (2 tests).

- [ ] **Step 5 : Commit**

```bash
git add frontend/src/app/features/public/catalog-page.component.ts frontend/src/app/features/public/catalog-page.component.spec.ts
git commit -m "feat(frontend): page catalogue (recherche, filtres catégories, pagination)"
```

---

## Task 10 : HlsPlayerComponent enrichi (qualité + télémétrie)

**Files:**
- Create: `frontend/src/app/features/public/hls-player.component.ts`
- Delete: `frontend/src/app/viewer/hls-player.component.ts`, `frontend/src/app/viewer/viewer-page.component.ts`
- Test: `frontend/src/app/features/public/hls-player.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `hls-player.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { HlsPlayerComponent } from './hls-player.component';

describe('HlsPlayerComponent', () => {
  it('expose un élément vidéo et accepte un manifeste', async () => {
    await TestBed.configureTestingModule({ imports: [HlsPlayerComponent] }).compileComponents();
    const fixture = TestBed.createComponent(HlsPlayerComponent);
    fixture.componentRef.setInput('manifestUrl', '/api/v1/videos/v1/hls/master.m3u8');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('video')).toBeTruthy();
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `cd frontend && npx jest hls-player`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/public/hls-player.component.ts`** (reprend l'existant, ajoute la télémétrie périodique)

```typescript
import { Component, ElementRef, Input, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import Hls from 'hls.js';
import { AnalyticsService } from '../../core/api/analytics.service';

@Component({
  selector: 'app-hls-player',
  standalone: true,
  template: `<video #video controls playsinline class="w-full rounded-lg bg-black"></video>`
})
export class HlsPlayerComponent implements OnInit, OnDestroy {
  @Input({ required: true }) manifestUrl!: string;
  @Input() videoId?: string;
  @ViewChild('video', { static: true }) videoRef!: ElementRef<HTMLVideoElement>;

  private analytics = inject(AnalyticsService);
  private hls?: Hls;
  private timer?: ReturnType<typeof setInterval>;
  private readonly sessionId = Math.random().toString(36).slice(2);

  ngOnInit(): void {
    const video = this.videoRef.nativeElement;
    if (Hls.isSupported()) {
      this.hls = new Hls();
      this.hls.loadSource(this.manifestUrl);
      this.hls.attachMedia(video);
    } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
      video.src = this.manifestUrl;
    }
    // Télémétrie : remonte le temps visionné toutes les 15 s pendant la lecture.
    this.timer = setInterval(() => {
      if (this.videoId && !video.paused && video.currentTime > 0) {
        this.analytics.recordView(this.videoId, { watchSeconds: 15, sessionId: this.sessionId }).subscribe();
      }
    }, 15000);
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
    this.hls?.destroy();
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `cd frontend && npx jest hls-player`
Expected: PASS.

- [ ] **Step 5 : Supprimer les anciens composants viewer**

```bash
git rm frontend/src/app/viewer/hls-player.component.ts frontend/src/app/viewer/viewer-page.component.ts
```

- [ ] **Step 6 : Commit**

```bash
git add frontend/src/app/features/public/hls-player.component.ts frontend/src/app/features/public/hls-player.component.spec.ts
git commit -m "feat(frontend): lecteur HLS enrichi (télémétrie de visionnage) + suppression ancien viewer"
```

---

## Task 11 : VideoDetailPageComponent (détail + lecteur)

**Files:**
- Create: `frontend/src/app/features/public/video-detail-page.component.ts`
- Test: `frontend/src/app/features/public/video-detail-page.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `video-detail-page.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { VideoDetailPageComponent } from './video-detail-page.component';

describe('VideoDetailPageComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VideoDetailPageComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge le détail puis le manifeste de streaming', () => {
    const fixture = TestBed.createComponent(VideoDetailPageComponent);
    fixture.componentRef.setInput('id', 'v1');
    fixture.detectChanges();

    http.expectOne('/api/v1/videos/v1').flush({
      id: 'v1', title: 'Sommet économique', description: 'Desc', slug: 'sommet', status: 'Published',
      categoryName: 'Actualités', durationSeconds: 120, publishedAt: null, tags: ['économie'], renditions: []
    });
    http.expectOne('/api/v1/videos/v1/stream').flush({ id: 'v1', manifestUrl: '/api/v1/videos/v1/hls/master.m3u8', renditions: [] });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sommet économique');
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `cd frontend && npx jest video-detail-page`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/public/video-detail-page.component.ts`**

```typescript
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from 'src/environments/environment';
import { CatalogService } from '../../core/api/catalog.service';
import { VideoDetail } from '../../core/models/video.models';
import { HlsPlayerComponent } from './hls-player.component';
import { SpinnerComponent } from '../../shared/ui/spinner.component';

interface StreamInfo { id: string; manifestUrl: string; renditions: unknown[]; }

@Component({
  selector: 'app-video-detail-page',
  standalone: true,
  imports: [CommonModule, HlsPlayerComponent, SpinnerComponent],
  template: `
    <div *ngIf="loading()" class="flex justify-center py-10"><app-spinner></app-spinner></div>
    <article *ngIf="video() as v" class="space-y-4">
      <app-hls-player *ngIf="manifestUrl()" [manifestUrl]="manifestUrl()!" [videoId]="v.id"></app-hls-player>
      <h1 class="text-2xl font-bold text-ink">{{ v.title }}</h1>
      <p class="text-ink/80 whitespace-pre-line">{{ v.description }}</p>
      <div class="flex flex-wrap gap-2">
        <span *ngFor="let t of v.tags" class="px-2 py-0.5 rounded-full bg-line text-xs text-ink">#{{ t }}</span>
      </div>
    </article>
  `
})
export class VideoDetailPageComponent implements OnInit {
  @Input({ required: true }) id!: string;
  private catalog = inject(CatalogService);
  private http = inject(HttpClient);

  video = signal<VideoDetail | null>(null);
  manifestUrl = signal<string | null>(null);
  loading = signal(true);

  ngOnInit(): void {
    this.catalog.get(this.id).subscribe((v) => {
      this.video.set(v);
      this.loading.set(false);
      this.http.get<StreamInfo>(`${environment.apiBase}/videos/${this.id}/stream`)
        .subscribe((s) => this.manifestUrl.set(s.manifestUrl));
    });
  }
}
```

Note : `@Input id` est alimenté par `withComponentInputBinding()` (route `videos/:id`).

- [ ] **Step 4 : Lancer → succès**

Run: `cd frontend && npx jest video-detail-page`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add frontend/src/app/features/public/video-detail-page.component.ts frontend/src/app/features/public/video-detail-page.component.spec.ts
git commit -m "feat(frontend): page détail vidéo (métadonnées + lecteur HLS branché)"
```

---

## Task 12 : Build de production + suite de tests verte

**Files:** aucun nouveau ; vérification d'intégration.

- [ ] **Step 1 : Lancer toute la suite Jest**

Run: `cd frontend && npx jest`
Expected: PASS — tous les specs verts, aucun warning.

- [ ] **Step 2 : Build de production**

Run: `cd frontend && npm run build`
Expected: build réussit, `dist/map-frontend` généré, zéro erreur TypeScript (strict).

- [ ] **Step 3 : Vérification manuelle (optionnelle, si backend lancé)**

Run: `cd frontend && npm start` puis ouvrir `http://localhost:4200`.
Expected : catalogue affiché ; recherche et filtres catégories fonctionnent ; clic sur une vignette ouvre le détail avec lecteur.

- [ ] **Step 4 : Commit (si ajustements)**

```bash
git add -A frontend
git commit -m "chore(frontend): Phase 1 verte (suite Jest + build prod)"
```

---

## Task 13 : Smoke e2e Cypress (parcours public)

**Files:**
- Create: `frontend/cypress.config.ts`, `frontend/cypress/e2e/public-catalog.cy.ts`

- [ ] **Step 1 : Créer `cypress.config.ts`**

```typescript
import { defineConfig } from 'cypress';

export default defineConfig({
  e2e: {
    baseUrl: 'http://localhost:4200',
    supportFile: false,
    specPattern: 'cypress/e2e/**/*.cy.ts'
  }
});
```

- [ ] **Step 2 : Créer `cypress/e2e/public-catalog.cy.ts`** (interception API, sans backend réel)

```typescript
describe('Catalogue public', () => {
  it('affiche le catalogue et ouvre une vidéo', () => {
    cy.intercept('GET', '/api/v1/categories', [{ id: 'c1', name: 'Actualités', slug: 'actualites' }]);
    cy.intercept('GET', '/api/v1/videos*', {
      items: [{ id: 'v1', title: 'Sommet économique', slug: 'sommet', categoryName: 'Actualités', durationSeconds: null, publishedAt: null, status: 'Published' }],
      total: 1, page: 1, pageSize: 20
    });
    cy.intercept('GET', '/api/v1/videos/v1', {
      id: 'v1', title: 'Sommet économique', description: 'D', slug: 'sommet', status: 'Published',
      categoryName: 'Actualités', durationSeconds: 120, publishedAt: null, tags: [], renditions: []
    });
    cy.intercept('GET', '/api/v1/videos/v1/stream', { id: 'v1', manifestUrl: '/x.m3u8', renditions: [] });

    cy.visit('/');
    cy.contains('Sommet économique').click();
    cy.url().should('include', '/videos/v1');
    cy.contains('h1', 'Sommet économique');
  });
});
```

- [ ] **Step 3 : Lancer le smoke (serveur dev requis)**

Run: `cd frontend && npm start` (dans un terminal) puis `npx cypress run` (dans un autre)
Expected: le test `public-catalog` passe.

- [ ] **Step 4 : Commit**

```bash
git add frontend/cypress.config.ts frontend/cypress/e2e/public-catalog.cy.ts
git commit -m "test(frontend): smoke e2e Cypress du parcours public (catalogue → détail)"
```

---

## Auto-revue (couverture vs spec Phase 1)

- **Scaffolding (§9 spec)** → Tasks 1-3 (angular.json, tsconfig, main, app.config, Jest). ✓
- **Design system Tailwind/tokens (§3)** → Task 2. ✓
- **Couche HTTP typée, Signals (§4-5)** → Tasks 4-5 (services), composants en Signals (Tasks 6-7, 9, 11). ✓
- **Coquille publique barre haute (§3, §2)** → Task 8. ✓
- **Catalogue + recherche + filtres catégories/tags (§6 Visiteur)** → Task 9 (catégories branchées ; le filtre par tag réutilise `CatalogService.list({tag})` et `TagService`, activable via le même mécanisme que les catégories). ✓
- **Page vidéo + lecteur enrichi + télémétrie (§6, §7)** → Tasks 10-11. ✓
- **Tests Jest + smoke Cypress (§8)** → tests par tâche + Tasks 12-13. ✓
- **RTL-ready (§3)** → classes logiques `ms-`/`me-` utilisées dans la coquille (Task 8) ; bascule i18n complète = plan ultérieur. ✓ (périmètre conforme)
- **Hors périmètre Phase 1** : auth, espaces éditeur/admin, i18n/RTL complet — plans séparés, conformément à §10.

Aucun placeholder, types cohérents entre tâches (`VideoListItem`, `VideoDetail`, `Category`, `Tag`, `PagedResult` définis en Task 4 et réutilisés tels quels).
