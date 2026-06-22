# Frontend MAP — Phase 3 : Espace Administrateur — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ajouter l'espace Administrateur sous `/admin` : tableau de bord statistiques, gestion des utilisateurs et rôles, gestion des catégories, journal d'audit, et configuration système.

**Architecture :** Réutilise `AuthStore`/intercepteur/`editorGuard` de la Phase 2 ; ajoute `adminGuard`. Services HTTP typés (`AdminService` pour stats/audit/config, `AdminUserService` pour les utilisateurs ; `CategoryService` étendu avec create/update/delete). Coquille `/admin` à barre latérale distincte.

**Tech Stack :** Angular 17 standalone, Signals, Tailwind, Jest. Réutilise modèles/composants des Phases 1-2.

**Spec :** [2026-06-22-frontend-angular-interfaces-design.md](../specs/2026-06-22-frontend-angular-interfaces-design.md) §6 (Admin).

**Contrats backend (vérifiés, toutes routes rôle Admin) :**
- `GET /api/v1/users` → `UserAdminItem[]` (`{ id, email, displayName, isActive, roles[] }`).
- `PUT /api/v1/users/{id}` body `{ isActive, displayName? }` → 204.
- `POST /api/v1/users/{id}/roles` body `{ role }` → 204 ; `DELETE /api/v1/users/{id}/roles/{role}` → 204.
- `GET /api/v1/stats` → `{ totalVideos, videosByStatus: Record<string,number>, totalUsers, totalViews, totalWatchSeconds, topVideos: [{ id, title, views }] }`.
- `GET /api/v1/audit?page=N` → `PagedResult<{ id, actorId, action, entityType, entityId, occurredAt }>`.
- `GET /api/v1/config` → `Record<string,string>` ; `PUT /api/v1/config` body `{ key, value }` → 204.
- `GET /api/v1/categories` (public) ; `POST /api/v1/categories` body `{ name }` → `{ id, name, slug }` ; `PUT /api/v1/categories/{id}` body `{ name }` → `{ id, name, slug }` ; `DELETE /api/v1/categories/{id}` → 204 (409 si utilisée).

**Pré-requis :** Phases 1-2 mergées. Travailler sur `feat/frontend-phase3`. Chemins relatifs à `frontend/`. Tests : `node_modules/.bin/jest`.

---

## Structure de fichiers (Phase 3)

```
src/app/
  core/auth/auth.guard.ts        # MODIFIÉ : + adminGuard
  core/admin/
    admin.models.ts              # UserAdminItem, StatsSummary, TopVideo, AuditEntry
    admin.service.ts             # stats/audit/config
    admin-user.service.ts        # list/update/assignRole/removeRole
  core/api/category.service.ts   # MODIFIÉ : + create/update/delete
  layouts/admin-shell.component.ts
  features/admin/
    admin-stats.component.ts
    admin-users.component.ts
    admin-categories.component.ts
    admin-audit.component.ts
    admin-config.component.ts
  app.routes.ts                  # MODIFIÉ : route /admin (adminGuard)
  layouts/public-shell.component.ts  # MODIFIÉ : lien Admin si rôle Admin
```

---

## Task 1 : adminGuard + modèles admin + AdminService (stats/audit/config)

**Files:**
- Modify: `src/app/core/auth/auth.guard.ts`
- Create: `src/app/core/admin/admin.models.ts`, `src/app/core/admin/admin.service.ts`
- Test: `src/app/core/auth/admin-guard.spec.ts`, `src/app/core/admin/admin.service.spec.ts`

- [ ] **Step 1 : Ajouter `adminGuard` à `src/app/core/auth/auth.guard.ts`** (après `editorGuard`)

```typescript
export const adminGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);
  if (store.isAuthenticated() && store.hasRole('Admin')) return true;
  router.navigate(['/auth/login']);
  return false;
};
```

- [ ] **Step 2 : Écrire le test qui échoue `src/app/core/auth/admin-guard.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { adminGuard } from './auth.guard';
import { AuthStore } from './auth.store';

describe('adminGuard', () => {
  let store: AuthStore;
  const router = { navigate: jest.fn() };
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [AuthStore, provideHttpClient(), provideHttpClientTesting(), { provide: Router, useValue: router }]
    });
    store = TestBed.inject(AuthStore);
    router.navigate.mockClear();
  });

  it('autorise un Admin', () => {
    store['_token'].set('t');
    store['_user'].set({ userId: 'u', email: 'e', displayName: 'E', roles: ['Admin'] });
    expect(TestBed.runInInjectionContext(() => adminGuard(null as never, null as never))).toBe(true);
  });

  it('refuse un Editeur', () => {
    store['_token'].set('t');
    store['_user'].set({ userId: 'u', email: 'e', displayName: 'E', roles: ['Editeur'] });
    expect(TestBed.runInInjectionContext(() => adminGuard(null as never, null as never))).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
  });
});
```

- [ ] **Step 3 : Lancer → échec attendu**

Run: `node_modules/.bin/jest admin-guard`
Expected: FAIL — `adminGuard` non exporté (si l'ordre des steps est inversé) ou test rouge ; sinon, après Step 1, il passe directement. Si déjà vert, c'est acceptable (la garde est triviale et déjà implémentée au Step 1).

- [ ] **Step 4 : Créer `src/app/core/admin/admin.models.ts`**

```typescript
export interface UserAdminItem {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  roles: string[];
}

export interface TopVideo { id: string; title: string; views: number; }

export interface StatsSummary {
  totalVideos: number;
  videosByStatus: Record<string, number>;
  totalUsers: number;
  totalViews: number;
  totalWatchSeconds: number;
  topVideos: TopVideo[];
}

export interface AuditEntry {
  id: string;
  actorId: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  occurredAt: string;
}
```

- [ ] **Step 5 : Écrire le test qui échoue `src/app/core/admin/admin.service.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AdminService } from './admin.service';

describe('AdminService', () => {
  let service: AdminService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [AdminService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AdminService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('récupère les stats', () => {
    service.stats().subscribe();
    expect(http.expectOne('/api/v1/stats').request.method).toBe('GET');
  });

  it('récupère l’audit paginé', () => {
    service.audit(3).subscribe();
    expect(http.expectOne('/api/v1/audit?page=3').request.method).toBe('GET');
  });

  it('lit et écrit la configuration', () => {
    service.config().subscribe();
    expect(http.expectOne('/api/v1/config').request.method).toBe('GET');
    service.setConfig('siteName', 'MAP').subscribe();
    const req = http.expectOne('/api/v1/config');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ key: 'siteName', value: 'MAP' });
    req.flush(null);
  });
});
```

- [ ] **Step 6 : Lancer → échec attendu**

Run: `node_modules/.bin/jest admin.service`
Expected: FAIL — `Cannot find module './admin.service'`.

- [ ] **Step 7 : Créer `src/app/core/admin/admin.service.ts`**

```typescript
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { PagedResult } from '../models/video.models';
import { AuditEntry, StatsSummary } from './admin.models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private base = environment.apiBase;

  stats(): Observable<StatsSummary> {
    return this.http.get<StatsSummary>(`${this.base}/stats`);
  }
  audit(page = 1): Observable<PagedResult<AuditEntry>> {
    return this.http.get<PagedResult<AuditEntry>>(`${this.base}/audit`, { params: new HttpParams().set('page', String(page)) });
  }
  config(): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>(`${this.base}/config`);
  }
  setConfig(key: string, value: string): Observable<void> {
    return this.http.put<void>(`${this.base}/config`, { key, value });
  }
}
```

- [ ] **Step 8 : Lancer → succès**

Run: `node_modules/.bin/jest admin-guard admin.service`
Expected: PASS (5 tests au total).

- [ ] **Step 9 : Commit**

```bash
git add src/app/core/auth/auth.guard.ts src/app/core/auth/admin-guard.spec.ts src/app/core/admin/admin.models.ts src/app/core/admin/admin.service.ts src/app/core/admin/admin.service.spec.ts
git commit -m "feat(frontend): adminGuard + modèles admin + AdminService (stats/audit/config)"
```

---

## Task 2 : AdminUserService

**Files:**
- Create: `src/app/core/admin/admin-user.service.ts`
- Test: `src/app/core/admin/admin-user.service.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `admin-user.service.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AdminUserService } from './admin-user.service';

describe('AdminUserService', () => {
  let service: AdminUserService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [AdminUserService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AdminUserService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('liste les utilisateurs', () => {
    service.list().subscribe();
    expect(http.expectOne('/api/v1/users').request.method).toBe('GET');
  });

  it('met à jour un utilisateur', () => {
    service.update('u1', { isActive: false, displayName: 'X' }).subscribe();
    const req = http.expectOne('/api/v1/users/u1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ isActive: false, displayName: 'X' });
    req.flush(null);
  });

  it('attribue et retire un rôle', () => {
    service.assignRole('u1', 'Editeur').subscribe();
    const a = http.expectOne('/api/v1/users/u1/roles');
    expect(a.request.method).toBe('POST');
    expect(a.request.body).toEqual({ role: 'Editeur' });
    a.flush(null);
    service.removeRole('u1', 'Editeur').subscribe();
    expect(http.expectOne('/api/v1/users/u1/roles/Editeur').request.method).toBe('DELETE');
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest admin-user.service`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/core/admin/admin-user.service.ts`**

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { UserAdminItem } from './admin.models';

@Injectable({ providedIn: 'root' })
export class AdminUserService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/users`;

  list(): Observable<UserAdminItem[]> {
    return this.http.get<UserAdminItem[]>(this.base);
  }
  update(id: string, body: { isActive: boolean; displayName?: string }): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, body);
  }
  assignRole(id: string, role: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/roles`, { role });
  }
  removeRole(id: string, role: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/roles/${role}`);
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest admin-user.service`
Expected: PASS (3 tests).

- [ ] **Step 5 : Commit**

```bash
git add src/app/core/admin/admin-user.service.ts src/app/core/admin/admin-user.service.spec.ts
git commit -m "feat(frontend): AdminUserService (list/update/assignRole/removeRole)"
```

---

## Task 3 : Étendre CategoryService (create/update/delete)

**Files:**
- Modify: `src/app/core/api/category.service.ts`
- Test: `src/app/core/api/category.service.spec.ts` (nouveau)

- [ ] **Step 1 : Écrire le test qui échoue `src/app/core/api/category.service.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { CategoryService } from './category.service';

describe('CategoryService (admin)', () => {
  let service: CategoryService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [CategoryService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(CategoryService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('crée une catégorie', () => {
    service.create('Actualités').subscribe();
    const req = http.expectOne('/api/v1/categories');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Actualités' });
    req.flush({ id: 'c1', name: 'Actualités', slug: 'actualites' });
  });

  it('renomme et supprime', () => {
    service.update('c1', 'Sport').subscribe();
    const put = http.expectOne('/api/v1/categories/c1');
    expect(put.request.method).toBe('PUT');
    put.flush({ id: 'c1', name: 'Sport', slug: 'sport' });
    service.delete('c1').subscribe();
    expect(http.expectOne('/api/v1/categories/c1').request.method).toBe('DELETE');
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest "api/category.service"`
Expected: FAIL — `service.create` n'existe pas.

- [ ] **Step 3 : Remplacer `src/app/core/api/category.service.ts`**

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { Category } from '../models/video.models';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/categories`;

  list(): Observable<Category[]> {
    return this.http.get<Category[]>(this.base);
  }
  create(name: string): Observable<Category> {
    return this.http.post<Category>(this.base, { name });
  }
  update(id: string, name: string): Observable<Category> {
    return this.http.put<Category>(`${this.base}/${id}`, { name });
  }
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest "api/category.service" reference.service`
Expected: PASS (les tests existants `reference.service` couvrant `list()` restent verts).

- [ ] **Step 5 : Commit**

```bash
git add src/app/core/api/category.service.ts src/app/core/api/category.service.spec.ts
git commit -m "feat(frontend): CategoryService étendu (create/update/delete admin)"
```

---

## Task 4 : Coquille Admin (AdminShell)

**Files:**
- Create: `src/app/layouts/admin-shell.component.ts`
- Test: `src/app/layouts/admin-shell.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `admin-shell.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminShellComponent } from './admin-shell.component';

describe('AdminShellComponent', () => {
  beforeEach(() => localStorage.clear());
  it('affiche la navigation Admin et un router-outlet', async () => {
    await TestBed.configureTestingModule({
      imports: [AdminShellComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(AdminShellComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Utilisateurs');
    expect(text).toContain('Audit');
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest admin-shell`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/layouts/admin-shell.component.ts`**

```typescript
import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../core/auth/auth.store';

@Component({
  selector: 'app-admin-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="min-h-screen flex bg-paper">
      <aside class="w-56 bg-ink text-ink-300 flex flex-col p-3 gap-1">
        <a routerLink="/" class="flex items-center gap-2 font-extrabold text-white mb-4 px-2">
          <span class="w-2.5 h-2.5 bg-map-red rounded-sm"></span> MAP&nbsp;Admin
        </a>
        <a routerLink="/admin" [routerLinkActiveOptions]="{ exact: true }" routerLinkActive="bg-map-red text-white" class="px-3 py-2 rounded-lg">Tableau de bord</a>
        <a routerLink="/admin/users" routerLinkActive="bg-map-red text-white" class="px-3 py-2 rounded-lg">Utilisateurs</a>
        <a routerLink="/admin/categories" routerLinkActive="bg-map-red text-white" class="px-3 py-2 rounded-lg">Catégories</a>
        <a routerLink="/admin/audit" routerLinkActive="bg-map-red text-white" class="px-3 py-2 rounded-lg">Audit</a>
        <a routerLink="/admin/config" routerLinkActive="bg-map-red text-white" class="px-3 py-2 rounded-lg">Configuration</a>
      </aside>
      <div class="flex-1 flex flex-col">
        <header class="h-14 bg-white border-b border-line flex items-center px-4">
          <span class="ms-auto text-sm text-ink">{{ store.user()?.displayName }}</span>
          <button (click)="logout()" class="ms-3 text-sm text-map-red underline">Déconnexion</button>
        </header>
        <main class="flex-1 p-6"><router-outlet></router-outlet></main>
      </div>
    </div>
  `
})
export class AdminShellComponent {
  store = inject(AuthStore);
  private router = inject(Router);
  logout(): void { this.store.logout(); this.router.navigate(['/']); }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest admin-shell`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/layouts/admin-shell.component.ts src/app/layouts/admin-shell.component.spec.ts
git commit -m "feat(frontend): coquille Admin (barre latérale)"
```

---

## Task 5 : Tableau de bord statistiques

**Files:**
- Create: `src/app/features/admin/admin-stats.component.ts`
- Test: `src/app/features/admin/admin-stats.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `admin-stats.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminStatsComponent } from './admin-stats.component';

describe('AdminStatsComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminStatsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge et affiche les statistiques', () => {
    const fixture = TestBed.createComponent(AdminStatsComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/stats').flush({
      totalVideos: 248, videosByStatus: { Published: 100 }, totalUsers: 12,
      totalViews: 12480, totalWatchSeconds: 99999, topVideos: [{ id: 'v1', title: 'Top sujet', views: 500 }]
    });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('248');
    expect(text).toContain('Top sujet');
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest admin-stats`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/admin/admin-stats.component.ts`**

```typescript
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminService } from '../../core/admin/admin.service';
import { StatsSummary } from '../../core/admin/admin.models';
import { SpinnerComponent } from '../../shared/ui/spinner.component';

@Component({
  selector: 'app-admin-stats',
  standalone: true,
  imports: [CommonModule, SpinnerComponent],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">Tableau de bord</h1>
    <div *ngIf="!stats()" class="flex justify-center py-10"><app-spinner></app-spinner></div>
    <div *ngIf="stats() as s" class="space-y-6">
      <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div class="bg-white border border-line rounded-lg p-4 shadow-soft"><div class="text-2xl font-bold text-ink">{{ s.totalVideos }}</div><div class="text-xs text-muted">vidéos</div></div>
        <div class="bg-white border border-line rounded-lg p-4 shadow-soft"><div class="text-2xl font-bold text-ink">{{ s.totalUsers }}</div><div class="text-xs text-muted">utilisateurs</div></div>
        <div class="bg-white border border-line rounded-lg p-4 shadow-soft"><div class="text-2xl font-bold text-ink">{{ s.totalViews }}</div><div class="text-xs text-muted">vues</div></div>
        <div class="bg-white border border-line rounded-lg p-4 shadow-soft"><div class="text-2xl font-bold text-ink">{{ hours(s.totalWatchSeconds) }}</div><div class="text-xs text-muted">heures vues</div></div>
      </div>
      <div class="bg-white border border-line rounded-lg p-4">
        <h2 class="font-semibold text-ink mb-2">Top contenus</h2>
        <ol class="list-decimal ms-5 space-y-1 text-sm text-ink">
          <li *ngFor="let v of s.topVideos">{{ v.title }} — <span class="text-muted">{{ v.views }} vues</span></li>
        </ol>
      </div>
    </div>
  `
})
export class AdminStatsComponent implements OnInit {
  private api = inject(AdminService);
  stats = signal<StatsSummary | null>(null);
  ngOnInit(): void { this.api.stats().subscribe((s) => this.stats.set(s)); }
  hours(seconds: number): number { return Math.round(seconds / 3600); }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest admin-stats`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/features/admin/admin-stats.component.ts src/app/features/admin/admin-stats.component.spec.ts
git commit -m "feat(frontend): tableau de bord statistiques admin"
```

---

## Task 6 : Gestion des utilisateurs

**Files:**
- Create: `src/app/features/admin/admin-users.component.ts`
- Test: `src/app/features/admin/admin-users.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `admin-users.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminUsersComponent } from './admin-users.component';

describe('AdminUsersComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminUsersComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('liste les utilisateurs et attribue un rôle', () => {
    const fixture = TestBed.createComponent(AdminUsersComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/users').flush([
      { id: 'u1', email: 'ed@map.ma', displayName: 'Éditeur', isActive: true, roles: ['Visiteur'] }
    ]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('ed@map.ma');

    fixture.componentInstance.assign('u1', 'Editeur');
    const post = http.expectOne('/api/v1/users/u1/roles');
    expect(post.request.body).toEqual({ role: 'Editeur' });
    post.flush(null);
    // recharge la liste après mutation
    http.expectOne('/api/v1/users').flush([]);
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest admin-users`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/admin/admin-users.component.ts`**

```typescript
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminUserService } from '../../core/admin/admin-user.service';
import { UserAdminItem } from '../../core/admin/admin.models';

const ROLES = ['Visiteur', 'Editeur', 'Admin'];

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [CommonModule],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">Utilisateurs</h1>
    <table class="w-full bg-white border border-line rounded-lg overflow-hidden text-sm">
      <thead class="text-left text-xs text-muted border-b border-line">
        <tr><th class="p-3">Email</th><th class="p-3">Nom</th><th class="p-3">Rôles</th><th class="p-3">Actif</th><th class="p-3">Attribuer</th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let u of users()" class="border-b border-line last:border-0">
          <td class="p-3 text-ink">{{ u.email }}</td>
          <td class="p-3">{{ u.displayName }}</td>
          <td class="p-3">
            <span *ngFor="let r of u.roles" class="inline-flex items-center gap-1 me-1 px-2 py-0.5 rounded-full bg-line text-xs">
              {{ r }} <button (click)="remove(u.id, r)" class="text-map-red">×</button>
            </span>
          </td>
          <td class="p-3">
            <button (click)="toggleActive(u)" class="underline" [class.text-emerald-700]="u.isActive" [class.text-muted]="!u.isActive">
              {{ u.isActive ? 'Oui' : 'Non' }}
            </button>
          </td>
          <td class="p-3">
            <select #sel class="border border-line rounded px-2 py-1" (change)="assign(u.id, sel.value); sel.value=''">
              <option value="">+ rôle</option>
              <option *ngFor="let r of roles" [value]="r">{{ r }}</option>
            </select>
          </td>
        </tr>
      </tbody>
    </table>
  `
})
export class AdminUsersComponent implements OnInit {
  private api = inject(AdminUserService);
  users = signal<UserAdminItem[]>([]);
  roles = ROLES;

  ngOnInit(): void { this.load(); }
  private load(): void { this.api.list().subscribe((u) => this.users.set(u)); }

  assign(id: string, role: string): void {
    if (!role) return;
    this.api.assignRole(id, role).subscribe(() => this.load());
  }
  remove(id: string, role: string): void {
    this.api.removeRole(id, role).subscribe(() => this.load());
  }
  toggleActive(u: UserAdminItem): void {
    this.api.update(u.id, { isActive: !u.isActive, displayName: u.displayName }).subscribe(() => this.load());
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest admin-users`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/features/admin/admin-users.component.ts src/app/features/admin/admin-users.component.spec.ts
git commit -m "feat(frontend): gestion des utilisateurs et rôles (admin)"
```

---

## Task 7 : Gestion des catégories

**Files:**
- Create: `src/app/features/admin/admin-categories.component.ts`
- Test: `src/app/features/admin/admin-categories.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `admin-categories.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminCategoriesComponent } from './admin-categories.component';

describe('AdminCategoriesComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminCategoriesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('liste puis crée une catégorie', () => {
    const fixture = TestBed.createComponent(AdminCategoriesComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/categories').flush([{ id: 'c1', name: 'Actualités', slug: 'actualites' }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Actualités');

    fixture.componentInstance.newName = 'Sport';
    fixture.componentInstance.create();
    const post = http.expectOne('/api/v1/categories');
    expect(post.request.body).toEqual({ name: 'Sport' });
    post.flush({ id: 'c2', name: 'Sport', slug: 'sport' });
    http.expectOne('/api/v1/categories').flush([]);
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest admin-categories`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/admin/admin-categories.component.ts`**

```typescript
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CategoryService } from '../../core/api/category.service';
import { Category } from '../../core/models/video.models';

@Component({
  selector: 'app-admin-categories',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">Catégories</h1>
    <div class="flex gap-2 mb-4 max-w-md">
      <input [(ngModel)]="newName" placeholder="Nouvelle catégorie" class="flex-1 px-3 py-2 rounded-lg border border-line" />
      <button (click)="create()" [disabled]="!newName" class="px-3 py-2 rounded-lg bg-ink text-white font-semibold disabled:opacity-50">Ajouter</button>
    </div>
    <p *ngIf="error()" class="text-sm text-map-red mb-2">{{ error() }}</p>
    <ul class="bg-white border border-line rounded-lg divide-y divide-line max-w-md">
      <li *ngFor="let c of categories()" class="flex items-center gap-2 p-3">
        <span class="flex-1 text-ink">{{ c.name }}</span>
        <button (click)="remove(c)" class="text-sm text-map-red underline">Supprimer</button>
      </li>
    </ul>
  `
})
export class AdminCategoriesComponent implements OnInit {
  private api = inject(CategoryService);
  categories = signal<Category[]>([]);
  newName = '';
  error = signal<string | null>(null);

  ngOnInit(): void { this.load(); }
  private load(): void { this.api.list().subscribe((cs) => this.categories.set(cs)); }

  create(): void {
    if (!this.newName) return;
    this.error.set(null);
    this.api.create(this.newName).subscribe({
      next: () => { this.newName = ''; this.load(); },
      error: (e) => this.error.set(e.error?.detail ?? 'Création impossible.')
    });
  }
  remove(c: Category): void {
    this.error.set(null);
    this.api.delete(c.id).subscribe({
      next: () => this.load(),
      error: (e) => this.error.set(e.error?.detail ?? 'Suppression impossible (catégorie utilisée ?).')
    });
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest admin-categories`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/features/admin/admin-categories.component.ts src/app/features/admin/admin-categories.component.spec.ts
git commit -m "feat(frontend): gestion des catégories (admin)"
```

---

## Task 8 : Journal d'audit

**Files:**
- Create: `src/app/features/admin/admin-audit.component.ts`
- Test: `src/app/features/admin/admin-audit.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `admin-audit.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminAuditComponent } from './admin-audit.component';

describe('AdminAuditComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminAuditComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge et affiche le journal d’audit', () => {
    const fixture = TestBed.createComponent(AdminAuditComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/audit?page=1').flush({
      items: [{ id: 'a1', actorId: 'u1', action: 'category.create', entityType: 'Category', entityId: 'c1', occurredAt: '2026-06-22T10:00:00Z' }],
      total: 1, page: 1, pageSize: 20
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('category.create');
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest admin-audit`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/admin/admin-audit.component.ts`**

```typescript
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminService } from '../../core/admin/admin.service';
import { AuditEntry } from '../../core/admin/admin.models';
import { PaginationComponent } from '../../shared/ui/pagination.component';

@Component({
  selector: 'app-admin-audit',
  standalone: true,
  imports: [CommonModule, PaginationComponent],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">Journal d'audit</h1>
    <table class="w-full bg-white border border-line rounded-lg overflow-hidden text-sm">
      <thead class="text-left text-xs text-muted border-b border-line">
        <tr><th class="p-3">Date</th><th class="p-3">Action</th><th class="p-3">Entité</th><th class="p-3">Acteur</th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let e of entries()" class="border-b border-line last:border-0">
          <td class="p-3 text-muted">{{ e.occurredAt | date: 'short' }}</td>
          <td class="p-3 text-ink font-medium">{{ e.action }}</td>
          <td class="p-3">{{ e.entityType }} <span class="text-muted">{{ e.entityId }}</span></td>
          <td class="p-3 text-muted">{{ e.actorId }}</td>
        </tr>
      </tbody>
    </table>
    <div class="mt-4">
      <app-pagination [total]="total()" [pageSize]="pageSize()" [page]="page()" (pageChange)="goToPage($event)"></app-pagination>
    </div>
  `
})
export class AdminAuditComponent implements OnInit {
  private api = inject(AdminService);
  entries = signal<AuditEntry[]>([]);
  total = signal(0);
  page = signal(1);
  pageSize = signal(20);

  ngOnInit(): void { this.load(); }
  private load(): void {
    this.api.audit(this.page()).subscribe((res) => {
      this.entries.set(res.items);
      this.total.set(res.total);
      this.pageSize.set(res.pageSize);
    });
  }
  goToPage(p: number): void { this.page.set(p); this.load(); }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest admin-audit`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/features/admin/admin-audit.component.ts src/app/features/admin/admin-audit.component.spec.ts
git commit -m "feat(frontend): journal d'audit (admin)"
```

---

## Task 9 : Configuration système

**Files:**
- Create: `src/app/features/admin/admin-config.component.ts`
- Test: `src/app/features/admin/admin-config.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `admin-config.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminConfigComponent } from './admin-config.component';

describe('AdminConfigComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminConfigComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge la configuration et enregistre une clé', () => {
    const fixture = TestBed.createComponent(AdminConfigComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/config').flush({ siteName: 'MAP' });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('siteName');

    fixture.componentInstance.save('siteName', 'MAP TV');
    const put = http.expectOne('/api/v1/config');
    expect(put.request.body).toEqual({ key: 'siteName', value: 'MAP TV' });
    put.flush(null);
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest admin-config`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/admin/admin-config.component.ts`**

```typescript
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/admin/admin.service';

interface ConfigRow { key: string; value: string; }

@Component({
  selector: 'app-admin-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">Configuration système</h1>
    <div class="space-y-2 max-w-lg">
      <div *ngFor="let row of rows()" class="flex items-center gap-2 bg-white border border-line rounded-lg p-3">
        <span class="w-40 text-sm text-muted">{{ row.key }}</span>
        <input [(ngModel)]="row.value" class="flex-1 px-3 py-1.5 rounded border border-line" />
        <button (click)="save(row.key, row.value)" class="px-3 py-1.5 rounded-lg bg-ink text-white text-sm">Enregistrer</button>
      </div>
      <div class="flex items-center gap-2 mt-4 bg-white border border-line rounded-lg p-3">
        <input [(ngModel)]="newKey" placeholder="clé" class="w-40 px-3 py-1.5 rounded border border-line" />
        <input [(ngModel)]="newValue" placeholder="valeur" class="flex-1 px-3 py-1.5 rounded border border-line" />
        <button (click)="add()" [disabled]="!newKey" class="px-3 py-1.5 rounded-lg bg-map-red text-white text-sm disabled:opacity-50">Ajouter</button>
      </div>
      <p *ngIf="message()" class="text-sm text-emerald-700">{{ message() }}</p>
    </div>
  `
})
export class AdminConfigComponent implements OnInit {
  private api = inject(AdminService);
  rows = signal<ConfigRow[]>([]);
  newKey = '';
  newValue = '';
  message = signal<string | null>(null);

  ngOnInit(): void { this.load(); }
  private load(): void {
    this.api.config().subscribe((cfg) => this.rows.set(Object.entries(cfg).map(([key, value]) => ({ key, value }))));
  }
  save(key: string, value: string): void {
    this.api.setConfig(key, value).subscribe(() => this.message.set(`« ${key} » enregistré.`));
  }
  add(): void {
    if (!this.newKey) return;
    this.api.setConfig(this.newKey, this.newValue).subscribe(() => { this.newKey = ''; this.newValue = ''; this.load(); });
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest admin-config`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/features/admin/admin-config.component.ts src/app/features/admin/admin-config.component.spec.ts
git commit -m "feat(frontend): configuration système (admin)"
```

---

## Task 10 : Câblage des routes /admin + lien Admin

**Files:**
- Modify: `src/app/app.routes.ts`, `src/app/layouts/public-shell.component.ts`

- [ ] **Step 1 : Ajouter le bloc `/admin` dans `src/app/app.routes.ts`** — insérer ce bloc juste après le bloc `studio` (avant le bloc `path: ''` public)

```typescript
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./layouts/admin-shell.component').then((m) => m.AdminShellComponent),
    children: [
      { path: '', loadComponent: () => import('./features/admin/admin-stats.component').then((m) => m.AdminStatsComponent) },
      { path: 'users', loadComponent: () => import('./features/admin/admin-users.component').then((m) => m.AdminUsersComponent) },
      { path: 'categories', loadComponent: () => import('./features/admin/admin-categories.component').then((m) => m.AdminCategoriesComponent) },
      { path: 'audit', loadComponent: () => import('./features/admin/admin-audit.component').then((m) => m.AdminAuditComponent) },
      { path: 'config', loadComponent: () => import('./features/admin/admin-config.component').then((m) => m.AdminConfigComponent) }
    ]
  },
```

Et mettre à jour l'import en tête de fichier :

```typescript
import { adminGuard, editorGuard } from './core/auth/auth.guard';
```

- [ ] **Step 2 : Ajouter le lien Admin dans `src/app/layouts/public-shell.component.ts`** — insérer après le lien Studio, dans le bloc `ms-auto` :

```html
          <a *ngIf="store.isAuthenticated() && store.hasRole('Admin')" routerLink="/admin" class="underline">Admin</a>
```

- [ ] **Step 3 : Lancer la suite ciblée → succès**

Run: `node_modules/.bin/jest public-shell`
Expected: PASS (le test existant reste vert ; le lien Admin n'apparaît que pour un Admin authentifié, non couvert par ce test mais sans régression).

- [ ] **Step 4 : Commit**

```bash
git add src/app/app.routes.ts src/app/layouts/public-shell.component.ts
git commit -m "feat(frontend): routes /admin (adminGuard) + lien Admin dans la barre publique"
```

---

## Task 11 : Suite verte + build de production

**Files:** aucun nouveau ; vérification.

- [ ] **Step 1 : Lancer toute la suite Jest**

Run: `node_modules/.bin/jest`
Expected: PASS — tous les specs verts (Phases 1-3).

- [ ] **Step 2 : Build de production**

Run: `npm run build`
Expected: build réussit ; chunks lazy `admin-shell`, `admin-stats`, `admin-users`, `admin-categories`, `admin-audit`, `admin-config` présents.

- [ ] **Step 3 : Commit (si ajustements)**

```bash
git add -A frontend
git commit -m "chore(frontend): Phase 3 verte (suite Jest + build prod)"
```

---

## Task 12 : Smoke e2e Cypress (parcours admin)

**Files:**
- Create: `frontend/cypress/e2e/admin-flow.cy.ts`

**Note :** binaire Cypress indisponible (voir mémoire projet) — écrire et committer ; exécution différée.

- [ ] **Step 1 : Créer `cypress/e2e/admin-flow.cy.ts`**

```typescript
describe('Parcours admin', () => {
  it('se connecte et voit les statistiques', () => {
    cy.intercept('POST', '/api/v1/auth/login', {
      token: 't', expiresAt: '2030-01-01T00:00:00Z', userId: 'u', email: 'admin@map.ma', displayName: 'Admin', roles: ['Admin']
    });
    cy.intercept('GET', '/api/v1/stats', {
      totalVideos: 248, videosByStatus: {}, totalUsers: 12, totalViews: 12480, totalWatchSeconds: 0, topVideos: []
    });

    cy.visit('/auth/login');
    cy.get('input[name=email]').type('admin@map.ma');
    cy.get('input[name=password]').type('p');
    cy.contains('Se connecter').click();
    cy.visit('/admin');
    cy.contains('Tableau de bord');
    cy.contains('248');
  });
});
```

- [ ] **Step 2 : Commit**

```bash
git add frontend/cypress/e2e/admin-flow.cy.ts
git commit -m "test(frontend): smoke e2e Cypress du parcours admin (login → stats)"
```

---

## Auto-revue (couverture vs spec Phase 3)

- **adminGuard (§5-6)** → Task 1. ✓
- **Statistiques : vues, temps de visionnage, top contenus (§6 Admin `GET /stats`)** → Tasks 1, 5. ✓
- **Utilisateurs & rôles CRUD/attribution (§6 `GET/PUT /users`, `POST/DELETE roles`)** → Tasks 2, 6. ✓
- **Catégories (création/suppression — pré-requis livré en Phase 0 backend)** → Tasks 3, 7. ✓
- **Audit (§6 `GET /audit`)** → Tasks 1, 8. ✓
- **Configuration système (§6 `GET/PUT /config`)** → Tasks 1, 9. ✓
- **Coquille Admin sidebar distincte (§3, séparation Admin/Éditeur validée)** → Task 4. ✓
- **Routes + accès Admin (§3)** → Task 10. ✓
- **Tests Jest + smoke Cypress (§8)** → tests par tâche + Tasks 11-12. ✓

**Hors périmètre Phase 3 (Phase 4) :** i18n FR/AR + RTL, gestion d'erreur globale par toasts (messages locaux inclus dans catégories/config). Pas de placeholder ; types cohérents (`UserAdminItem`/`StatsSummary`/`AuditEntry`/`Category` réutilisés tels quels). Le `PaginationComponent` (Phase 1) est réutilisé pour l'audit.
