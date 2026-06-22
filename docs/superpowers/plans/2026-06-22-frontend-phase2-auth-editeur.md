# Frontend MAP — Phase 2 : Auth & Espace Éditeur — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ajouter l'authentification (login/register JWT, garde de rôles, intercepteur) et l'espace Éditeur (coquille à barre latérale, tableau de bord « mes vidéos », upload chunké + suivi transcodage, édition de métadonnées, publication/archivage).

**Architecture :** `AuthStore` basé Signals (token persistant en `localStorage`, JWT unique sans refresh), intercepteur HTTP fonctionnel qui attache le Bearer et gère le 401, gardes fonctionnelles (`authGuard`, `editorGuard`). Espace Éditeur sous `/studio` avec sa propre coquille. Services HTTP typés par domaine.

**Tech Stack :** Angular 17 standalone, Signals, RxJS, Tailwind, Jest. Réutilise les modèles/composants de la Phase 1.

**Spec :** [2026-06-22-frontend-angular-interfaces-design.md](../specs/2026-06-22-frontend-angular-interfaces-design.md) §4, §5, §6 (Éditeur).

**Contrats backend (vérifiés) :**
- `POST /api/v1/auth/login` body `{ email, password }` → `AuthResponse { token, expiresAt, userId, email, displayName, roles[] }` (401 si invalide).
- `POST /api/v1/auth/register` body `{ email, password, displayName }` → `AuthResponse` (409 si email déjà utilisé). Le compte créé a le rôle **Visiteur** par défaut.
- `GET /api/v1/auth/me` (Bearer) → `{ userId, email, displayName, roles[] }`.
- `GET /api/v1/videos/mine?page=N` (rôle Editeur/Admin) → `PagedResult<VideoListItem>`.
- `POST /api/v1/videos` (rôle Editeur) body `{ title, description?, categoryId? }` → `{ id }`.
- `POST /api/v1/videos/{id}/upload/chunk?index=N` (rôle Editeur) body = octets bruts → `{ received }`.
- `POST /api/v1/videos/{id}/upload/complete?total=N` (rôle Editeur) → `{ id, status }`.
- `PUT /api/v1/videos/{id}` (Editeur/Admin) body `{ title, description?, categoryId?, tags[] }` → `VideoDetail`.
- `POST /api/v1/videos/{id}/publish` · `/archive` (Editeur/Admin) → 200.

**Pré-requis :** Phase 1 livrée (mergée dans master). Travailler sur une nouvelle branche `feat/frontend-phase2`. Tous les chemins sont relatifs à `frontend/`. Lancer les tests avec `node_modules/.bin/jest` (le binaire Cypress est indisponible ici — voir mémoire projet).

---

## Structure de fichiers (Phase 2)

```
src/app/
  core/auth/
    auth.models.ts          # AuthResponse, AuthUser, LoginRequest, RegisterRequest
    auth.service.ts         # HTTP : login, register, me
    auth.store.ts           # Signals : token/user/roles, login/register/logout/loadSession
    auth.interceptor.ts     # intercepteur fonctionnel : Bearer + gestion 401
    auth.guard.ts           # authGuard, editorGuard (CanActivateFn)
  core/api/
    editor-video.service.ts # mine/create/uploadChunk/complete/update/publish/archive
  features/auth/
    login-page.component.ts
    register-page.component.ts
  features/editor/
    editor-dashboard.component.ts
    video-upload.component.ts
    video-edit.component.ts
  layouts/editor-shell.component.ts
  app.routes.ts             # MODIFIÉ : routes /auth/* et /studio/*
  app.config.ts             # MODIFIÉ : withInterceptors([authInterceptor])
  layouts/public-shell.component.ts  # MODIFIÉ : lien Connexion → /auth/login
```

---

## Task 1 : Modèles auth + AuthService

**Files:**
- Create: `src/app/core/auth/auth.models.ts`, `src/app/core/auth/auth.service.ts`
- Test: `src/app/core/auth/auth.service.spec.ts`

- [ ] **Step 1 : Créer `src/app/core/auth/auth.models.ts`**

```typescript
export interface AuthResponse {
  token: string;
  expiresAt: string;
  userId: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface AuthUser {
  userId: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface LoginRequest { email: string; password: string; }
export interface RegisterRequest { email: string; password: string; displayName: string; }
```

- [ ] **Step 2 : Écrire le test qui échoue `auth.service.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AuthService } from './auth.service';

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [AuthService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('poste les identifiants au login', () => {
    service.login({ email: 'a@map.ma', password: 'p' }).subscribe();
    const req = http.expectOne('/api/v1/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'a@map.ma', password: 'p' });
    req.flush({});
  });

  it('poste l’inscription', () => {
    service.register({ email: 'a@map.ma', password: 'p', displayName: 'A' }).subscribe();
    expect(http.expectOne('/api/v1/auth/register').request.method).toBe('POST');
  });

  it('récupère la session courante', () => {
    service.me().subscribe();
    expect(http.expectOne('/api/v1/auth/me').request.method).toBe('GET');
  });
});
```

- [ ] **Step 3 : Lancer → échec attendu**

Run: `node_modules/.bin/jest auth.service`
Expected: FAIL — `Cannot find module './auth.service'`.

- [ ] **Step 4 : Créer `src/app/core/auth/auth.service.ts`**

```typescript
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { AuthResponse, AuthUser, LoginRequest, RegisterRequest } from './auth.models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/auth`;

  login(body: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/login`, body);
  }
  register(body: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.base}/register`, body);
  }
  me(): Observable<AuthUser> {
    return this.http.get<AuthUser>(`${this.base}/me`);
  }
}
```

- [ ] **Step 5 : Lancer → succès**

Run: `node_modules/.bin/jest auth.service`
Expected: PASS (3 tests).

- [ ] **Step 6 : Commit**

```bash
git add src/app/core/auth/auth.models.ts src/app/core/auth/auth.service.ts src/app/core/auth/auth.service.spec.ts
git commit -m "feat(frontend): modèles auth + AuthService (login/register/me)"
```

---

## Task 2 : AuthStore (Signals, persistance localStorage)

**Files:**
- Create: `src/app/core/auth/auth.store.ts`
- Test: `src/app/core/auth/auth.store.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `auth.store.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AuthStore } from './auth.store';
import { AuthResponse } from './auth.models';

const FAKE: AuthResponse = {
  token: 'jwt-123', expiresAt: '2030-01-01T00:00:00Z', userId: 'u1',
  email: 'ed@map.ma', displayName: 'Éditeur', roles: ['Editeur']
};

describe('AuthStore', () => {
  let store: AuthStore;
  let http: HttpTestingController;
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [AuthStore, provideHttpClient(), provideHttpClientTesting()] });
    store = TestBed.inject(AuthStore);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('au login : stocke le token, l’utilisateur, et persiste', () => {
    store.login({ email: 'ed@map.ma', password: 'p' }).subscribe();
    http.expectOne('/api/v1/auth/login').flush(FAKE);
    expect(store.isAuthenticated()).toBe(true);
    expect(store.user()?.displayName).toBe('Éditeur');
    expect(store.hasRole('Editeur')).toBe(true);
    expect(localStorage.getItem('map_token')).toBe('jwt-123');
  });

  it('au logout : purge tout', () => {
    store.login({ email: 'ed@map.ma', password: 'p' }).subscribe();
    http.expectOne('/api/v1/auth/login').flush(FAKE);
    store.logout();
    expect(store.isAuthenticated()).toBe(false);
    expect(store.user()).toBeNull();
    expect(localStorage.getItem('map_token')).toBeNull();
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest auth.store`
Expected: FAIL — `Cannot find module './auth.store'`.

- [ ] **Step 3 : Créer `src/app/core/auth/auth.store.ts`**

```typescript
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { AuthService } from './auth.service';
import { AuthResponse, AuthUser, LoginRequest, RegisterRequest } from './auth.models';

const TOKEN_KEY = 'map_token';

@Injectable({ providedIn: 'root' })
export class AuthStore {
  private auth = inject(AuthService);
  private _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  private _user = signal<AuthUser | null>(null);

  token = this._token.asReadonly();
  user = this._user.asReadonly();
  isAuthenticated = computed(() => !!this._token());
  roles = computed(() => this._user()?.roles ?? []);

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  login(body: LoginRequest): Observable<AuthResponse> {
    return this.auth.login(body).pipe(tap((res) => this.apply(res)));
  }
  register(body: RegisterRequest): Observable<AuthResponse> {
    return this.auth.register(body).pipe(tap((res) => this.apply(res)));
  }
  loadSession(): Observable<AuthUser> {
    return this.auth.me().pipe(tap((u) => this._user.set(u)));
  }
  logout(): void {
    this._token.set(null);
    this._user.set(null);
    localStorage.removeItem(TOKEN_KEY);
  }

  private apply(res: AuthResponse): void {
    this._token.set(res.token);
    localStorage.setItem(TOKEN_KEY, res.token);
    this._user.set({ userId: res.userId, email: res.email, displayName: res.displayName, roles: res.roles });
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest auth.store`
Expected: PASS (2 tests).

- [ ] **Step 5 : Commit**

```bash
git add src/app/core/auth/auth.store.ts src/app/core/auth/auth.store.spec.ts
git commit -m "feat(frontend): AuthStore (signals, persistance localStorage, hasRole)"
```

---

## Task 3 : Intercepteur HTTP (Bearer + 401)

**Files:**
- Create: `src/app/core/auth/auth.interceptor.ts`
- Modify: `src/app/app.config.ts`
- Test: `src/app/core/auth/auth.interceptor.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `auth.interceptor.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthStore } from './auth.store';

describe('authInterceptor', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  let store: AuthStore;
  const router = { navigate: jest.fn() };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: Router, useValue: router }
      ]
    });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
    store = TestBed.inject(AuthStore);
  });
  afterEach(() => http.verify());

  it('ajoute l’en-tête Authorization quand un token est présent', () => {
    localStorage.setItem('map_token', 'jwt-xyz');
    // recrée le store pour relire le token persistant
    const s = TestBed.inject(AuthStore);
    s['_token'].set('jwt-xyz');
    client.get('/api/v1/videos/mine').subscribe();
    const req = http.expectOne('/api/v1/videos/mine');
    expect(req.request.headers.get('Authorization')).toBe('Bearer jwt-xyz');
    req.flush({});
  });

  it('déconnecte et redirige au 401', () => {
    store['_token'].set('jwt-xyz');
    client.get('/api/v1/videos/mine').subscribe({ error: () => {} });
    http.expectOne('/api/v1/videos/mine').flush('nope', { status: 401, statusText: 'Unauthorized' });
    expect(store.isAuthenticated()).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest auth.interceptor`
Expected: FAIL — `Cannot find module './auth.interceptor'`.

- [ ] **Step 3 : Créer `src/app/core/auth/auth.interceptor.ts`**

```typescript
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthStore } from './auth.store';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const store = inject(AuthStore);
  const router = inject(Router);
  const token = store.token();
  const authReq = token
    ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
    : req;

  return next(authReq).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401) {
        store.logout();
        router.navigate(['/auth/login']);
      }
      return throwError(() => err);
    })
  );
};
```

- [ ] **Step 4 : Modifier `src/app/app.config.ts`** pour enregistrer l'intercepteur

Remplacer la ligne `provideHttpClient(withFetch())` par :

```typescript
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor]))
  ]
};
```

- [ ] **Step 5 : Lancer → succès**

Run: `node_modules/.bin/jest auth.interceptor`
Expected: PASS (2 tests).

- [ ] **Step 6 : Commit**

```bash
git add src/app/core/auth/auth.interceptor.ts src/app/core/auth/auth.interceptor.spec.ts src/app/app.config.ts
git commit -m "feat(frontend): intercepteur HTTP (Bearer + déconnexion sur 401)"
```

---

## Task 4 : Gardes de route (authGuard, editorGuard)

**Files:**
- Create: `src/app/core/auth/auth.guard.ts`
- Test: `src/app/core/auth/auth.guard.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `auth.guard.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { authGuard, editorGuard } from './auth.guard';
import { AuthStore } from './auth.store';

describe('gardes', () => {
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

  it('authGuard refuse l’anonyme et redirige', () => {
    const ok = TestBed.runInInjectionContext(() => authGuard(null as never, null as never));
    expect(ok).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
  });

  it('editorGuard autorise un Editeur authentifié', () => {
    store['_token'].set('t');
    store['_user'].set({ userId: 'u', email: 'e', displayName: 'E', roles: ['Editeur'] });
    const ok = TestBed.runInInjectionContext(() => editorGuard(null as never, null as never));
    expect(ok).toBe(true);
  });

  it('editorGuard refuse un simple Visiteur', () => {
    store['_token'].set('t');
    store['_user'].set({ userId: 'u', email: 'e', displayName: 'E', roles: ['Visiteur'] });
    const ok = TestBed.runInInjectionContext(() => editorGuard(null as never, null as never));
    expect(ok).toBe(false);
    expect(router.navigate).toHaveBeenCalledWith(['/auth/login']);
  });
});
```

Note : on supprime la fonction `run` inutile et on utilise `TestBed.runInInjectionContext` (disponible en Angular 17) pour exécuter la garde dans un contexte d'injection. Retirer la ligne `const run = ...` si l'éditeur la signale ; elle n'est pas utilisée par les tests ci-dessus.

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest auth.guard`
Expected: FAIL — `Cannot find module './auth.guard'`.

- [ ] **Step 3 : Créer `src/app/core/auth/auth.guard.ts`**

```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthStore } from './auth.store';

export const authGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);
  if (store.isAuthenticated()) return true;
  router.navigate(['/auth/login']);
  return false;
};

export const editorGuard: CanActivateFn = () => {
  const store = inject(AuthStore);
  const router = inject(Router);
  if (store.isAuthenticated() && (store.hasRole('Editeur') || store.hasRole('Admin'))) return true;
  router.navigate(['/auth/login']);
  return false;
};
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest auth.guard`
Expected: PASS (3 tests).

- [ ] **Step 5 : Commit**

```bash
git add src/app/core/auth/auth.guard.ts src/app/core/auth/auth.guard.spec.ts
git commit -m "feat(frontend): gardes authGuard + editorGuard"
```

---

## Task 5 : Page de connexion

**Files:**
- Create: `src/app/features/auth/login-page.component.ts`
- Test: `src/app/features/auth/login-page.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `login-page.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { LoginPageComponent } from './login-page.component';

describe('LoginPageComponent', () => {
  let http: HttpTestingController;
  const router = { navigate: jest.fn() };
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [LoginPageComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: Router, useValue: router }]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    router.navigate.mockClear();
  });

  it('connecte puis redirige vers /studio', () => {
    const fixture = TestBed.createComponent(LoginPageComponent);
    const cmp = fixture.componentInstance;
    fixture.detectChanges();
    cmp.email = 'ed@map.ma'; cmp.password = 'p';
    cmp.submit();
    http.expectOne('/api/v1/auth/login').flush({
      token: 't', expiresAt: '2030-01-01T00:00:00Z', userId: 'u', email: 'ed@map.ma', displayName: 'Ed', roles: ['Editeur']
    });
    expect(router.navigate).toHaveBeenCalledWith(['/studio']);
  });

  it('affiche une erreur au 401', () => {
    const fixture = TestBed.createComponent(LoginPageComponent);
    const cmp = fixture.componentInstance;
    fixture.detectChanges();
    cmp.submit();
    http.expectOne('/api/v1/auth/login').flush({ detail: 'Identifiants invalides.' }, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();
    expect(cmp.error()).toContain('Identifiants');
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest login-page`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/auth/login-page.component.ts`**

```typescript
import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'app-login-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="max-w-sm mx-auto mt-16 bg-white border border-line rounded-lg shadow-soft p-6">
      <h1 class="text-xl font-bold text-ink mb-4">Connexion</h1>
      <form (ngSubmit)="submit()" class="space-y-3">
        <input [(ngModel)]="email" name="email" type="email" required placeholder="Email"
               class="w-full px-3 py-2 rounded-lg border border-line" />
        <input [(ngModel)]="password" name="password" type="password" required placeholder="Mot de passe"
               class="w-full px-3 py-2 rounded-lg border border-line" />
        <p *ngIf="error()" class="text-sm text-map-red">{{ error() }}</p>
        <button type="submit" [disabled]="loading()"
                class="w-full py-2 rounded-lg bg-ink text-white font-semibold disabled:opacity-50">Se connecter</button>
      </form>
      <p class="mt-3 text-sm text-muted">Pas de compte ? <a routerLink="/auth/register" class="text-ink underline">Créer un compte</a></p>
    </div>
  `
})
export class LoginPageComponent {
  private store = inject(AuthStore);
  private router = inject(Router);
  email = '';
  password = '';
  loading = signal(false);
  error = signal<string | null>(null);

  submit(): void {
    this.loading.set(true);
    this.error.set(null);
    this.store.login({ email: this.email, password: this.password }).subscribe({
      next: () => { this.loading.set(false); this.router.navigate(['/studio']); },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(e.error?.detail ?? 'Identifiants invalides.');
      }
    });
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest login-page`
Expected: PASS (2 tests).

- [ ] **Step 5 : Commit**

```bash
git add src/app/features/auth/login-page.component.ts src/app/features/auth/login-page.component.spec.ts
git commit -m "feat(frontend): page de connexion (login + gestion d'erreur)"
```

---

## Task 6 : Page d'inscription

**Files:**
- Create: `src/app/features/auth/register-page.component.ts`
- Test: `src/app/features/auth/register-page.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `register-page.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RegisterPageComponent } from './register-page.component';

describe('RegisterPageComponent', () => {
  let http: HttpTestingController;
  const router = { navigate: jest.fn() };
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [RegisterPageComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: Router, useValue: router }]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
    router.navigate.mockClear();
  });

  it('inscrit puis redirige vers l’accueil', () => {
    const fixture = TestBed.createComponent(RegisterPageComponent);
    const cmp = fixture.componentInstance;
    fixture.detectChanges();
    cmp.email = 'v@map.ma'; cmp.password = 'p'; cmp.displayName = 'Visiteur';
    cmp.submit();
    http.expectOne('/api/v1/auth/register').flush({
      token: 't', expiresAt: '2030-01-01T00:00:00Z', userId: 'u', email: 'v@map.ma', displayName: 'Visiteur', roles: ['Visiteur']
    });
    expect(router.navigate).toHaveBeenCalledWith(['/']);
  });

  it('affiche une erreur au 409', () => {
    const fixture = TestBed.createComponent(RegisterPageComponent);
    const cmp = fixture.componentInstance;
    fixture.detectChanges();
    cmp.submit();
    http.expectOne('/api/v1/auth/register').flush({ detail: 'Email déjà utilisé.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    expect(cmp.error()).toContain('Email');
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest register-page`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/auth/register-page.component.ts`**

```typescript
import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { AuthStore } from '../../core/auth/auth.store';

@Component({
  selector: 'app-register-page',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="max-w-sm mx-auto mt-16 bg-white border border-line rounded-lg shadow-soft p-6">
      <h1 class="text-xl font-bold text-ink mb-4">Créer un compte</h1>
      <form (ngSubmit)="submit()" class="space-y-3">
        <input [(ngModel)]="displayName" name="displayName" required placeholder="Nom affiché"
               class="w-full px-3 py-2 rounded-lg border border-line" />
        <input [(ngModel)]="email" name="email" type="email" required placeholder="Email"
               class="w-full px-3 py-2 rounded-lg border border-line" />
        <input [(ngModel)]="password" name="password" type="password" required placeholder="Mot de passe"
               class="w-full px-3 py-2 rounded-lg border border-line" />
        <p *ngIf="error()" class="text-sm text-map-red">{{ error() }}</p>
        <button type="submit" [disabled]="loading()"
                class="w-full py-2 rounded-lg bg-ink text-white font-semibold disabled:opacity-50">S'inscrire</button>
      </form>
      <p class="mt-3 text-sm text-muted">Déjà inscrit ? <a routerLink="/auth/login" class="text-ink underline">Se connecter</a></p>
    </div>
  `
})
export class RegisterPageComponent {
  private store = inject(AuthStore);
  private router = inject(Router);
  email = '';
  password = '';
  displayName = '';
  loading = signal(false);
  error = signal<string | null>(null);

  submit(): void {
    this.loading.set(true);
    this.error.set(null);
    this.store.register({ email: this.email, password: this.password, displayName: this.displayName }).subscribe({
      next: () => { this.loading.set(false); this.router.navigate(['/']); },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(e.error?.detail ?? 'Inscription impossible.');
      }
    });
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest register-page`
Expected: PASS (2 tests).

- [ ] **Step 5 : Commit**

```bash
git add src/app/features/auth/register-page.component.ts src/app/features/auth/register-page.component.spec.ts
git commit -m "feat(frontend): page d'inscription (register + gestion d'erreur)"
```

---

## Task 7 : Routes auth + lien Connexion dans la coquille publique

**Files:**
- Modify: `src/app/app.routes.ts`, `src/app/layouts/public-shell.component.ts`
- Test: `src/app/layouts/public-shell.component.spec.ts` (mise à jour)

- [ ] **Step 1 : Modifier `src/app/app.routes.ts`** — ajouter les routes `auth` avant le `**`

Remplacer le contenu par :

```typescript
import { Routes } from '@angular/router';
import { editorGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'auth/login',
    loadComponent: () => import('./features/auth/login-page.component').then((m) => m.LoginPageComponent)
  },
  {
    path: 'auth/register',
    loadComponent: () => import('./features/auth/register-page.component').then((m) => m.RegisterPageComponent)
  },
  {
    path: 'studio',
    canActivate: [editorGuard],
    loadComponent: () => import('./layouts/editor-shell.component').then((m) => m.EditorShellComponent),
    children: [
      { path: '', loadComponent: () => import('./features/editor/editor-dashboard.component').then((m) => m.EditorDashboardComponent) },
      { path: 'upload', loadComponent: () => import('./features/editor/video-upload.component').then((m) => m.VideoUploadComponent) },
      { path: 'videos/:id/edit', loadComponent: () => import('./features/editor/video-edit.component').then((m) => m.VideoEditComponent) }
    ]
  },
  {
    path: '',
    loadComponent: () => import('./layouts/public-shell.component').then((m) => m.PublicShellComponent),
    children: [
      { path: '', loadComponent: () => import('./features/public/catalog-page.component').then((m) => m.CatalogPageComponent) },
      { path: 'videos/:id', loadComponent: () => import('./features/public/video-detail-page.component').then((m) => m.VideoDetailPageComponent) }
    ]
  },
  { path: '**', redirectTo: '' }
];
```

- [ ] **Step 2 : Modifier `src/app/layouts/public-shell.component.ts`** — rendre le bouton Connexion cliquable et afficher l'utilisateur

Remplacer le bloc `<div class="ms-auto ...">` par :

```html
        <div class="ms-auto flex items-center gap-3 text-sm">
          <span class="bg-ink-500 rounded-md px-2 py-1 font-semibold">FR · ع</span>
          <a *ngIf="!store.isAuthenticated()" routerLink="/auth/login" class="bg-map-red rounded-lg px-3 py-1.5 font-semibold">Connexion</a>
          <a *ngIf="store.isAuthenticated() && (store.hasRole('Editeur') || store.hasRole('Admin'))" routerLink="/studio" class="underline">Studio</a>
          <button *ngIf="store.isAuthenticated()" (click)="store.logout()" class="underline">Déconnexion</button>
        </div>
```

Et mettre à jour la classe du composant + imports :

```typescript
import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AuthStore } from '../core/auth/auth.store';

@Component({
  selector: 'app-public-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, CommonModule],
  template: ` ... (le template complet avec le bloc ms-auto ci-dessus) ... `
})
export class PublicShellComponent {
  store = inject(AuthStore);
}
```

Le template complet :

```html
    <div class="min-h-screen flex flex-col bg-paper">
      <header class="h-14 bg-ink text-white flex items-center gap-4 px-4">
        <a routerLink="/" class="flex items-center gap-2 font-extrabold">
          <span class="w-2.5 h-2.5 bg-map-red rounded-sm"></span> MAP&nbsp;Vidéo
        </a>
        <div class="ms-auto flex items-center gap-3 text-sm">
          <span class="bg-ink-500 rounded-md px-2 py-1 font-semibold">FR · ع</span>
          <a *ngIf="!store.isAuthenticated()" routerLink="/auth/login" class="bg-map-red rounded-lg px-3 py-1.5 font-semibold">Connexion</a>
          <a *ngIf="store.isAuthenticated() && (store.hasRole('Editeur') || store.hasRole('Admin'))" routerLink="/studio" class="underline">Studio</a>
          <button *ngIf="store.isAuthenticated()" (click)="store.logout()" class="underline">Déconnexion</button>
        </div>
      </header>
      <main class="flex-1 max-w-6xl w-full mx-auto px-4 py-6">
        <router-outlet></router-outlet>
      </main>
      <footer class="text-center text-xs text-muted py-4">Maghreb Arabe Presse — plateforme vidéo interne</footer>
    </div>
```

- [ ] **Step 3 : Mettre à jour `public-shell.component.spec.ts`** — fournir les dépendances HTTP (AuthStore les injecte)

Remplacer le contenu par :

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { PublicShellComponent } from './public-shell.component';

describe('PublicShellComponent', () => {
  beforeEach(() => localStorage.clear());

  it('affiche la barre MAP, un lien Connexion (anonyme) et un router-outlet', async () => {
    await TestBed.configureTestingModule({
      imports: [PublicShellComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(PublicShellComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('MAP');
    expect(text).toContain('Connexion');
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });
});
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest public-shell`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/app.routes.ts src/app/layouts/public-shell.component.ts src/app/layouts/public-shell.component.spec.ts
git commit -m "feat(frontend): routes auth/studio + Connexion/Déconnexion dans la coquille publique"
```

---

## Task 8 : EditorVideoService

**Files:**
- Create: `src/app/core/api/editor-video.service.ts`
- Test: `src/app/core/api/editor-video.service.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `editor-video.service.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { EditorVideoService } from './editor-video.service';

describe('EditorVideoService', () => {
  let service: EditorVideoService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [EditorVideoService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(EditorVideoService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('liste mes vidéos paginées', () => {
    service.mine(2).subscribe();
    expect(http.expectOne('/api/v1/videos/mine?page=2').request.method).toBe('GET');
  });

  it('crée un brouillon', () => {
    service.create({ title: 'T', description: 'D', categoryId: 'c1' }).subscribe();
    const req = http.expectOne('/api/v1/videos');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ title: 'T', description: 'D', categoryId: 'c1' });
    req.flush({ id: 'v1' });
  });

  it('téléverse un chunk avec index', () => {
    const blob = new Blob([new Uint8Array([1, 2, 3])]);
    service.uploadChunk('v1', 0, blob).subscribe();
    const req = http.expectOne('/api/v1/videos/v1/upload/chunk?index=0');
    expect(req.request.method).toBe('POST');
    req.flush({ received: 3 });
  });

  it('finalise l’upload avec total', () => {
    service.complete('v1', 2).subscribe();
    const req = http.expectOne('/api/v1/videos/v1/upload/complete?total=2');
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'v1', status: 'Processing' });
  });

  it('met à jour les métadonnées', () => {
    service.update('v1', { title: 'T2', description: null, categoryId: null, tags: ['x'] }).subscribe();
    const req = http.expectOne('/api/v1/videos/v1');
    expect(req.request.method).toBe('PUT');
    req.flush({});
  });

  it('publie et archive', () => {
    service.publish('v1').subscribe();
    expect(http.expectOne('/api/v1/videos/v1/publish').request.method).toBe('POST');
    service.archive('v1').subscribe();
    expect(http.expectOne('/api/v1/videos/v1/archive').request.method).toBe('POST');
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest editor-video.service`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/core/api/editor-video.service.ts`**

```typescript
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { PagedResult, VideoDetail, VideoListItem } from '../models/video.models';

export interface CreateVideoBody { title: string; description?: string; categoryId?: string; }
export interface UpdateVideoBody { title: string; description: string | null; categoryId: string | null; tags: string[]; }

@Injectable({ providedIn: 'root' })
export class EditorVideoService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/videos`;

  mine(page = 1): Observable<PagedResult<VideoListItem>> {
    return this.http.get<PagedResult<VideoListItem>>(`${this.base}/mine`, { params: new HttpParams().set('page', String(page)) });
  }
  create(body: CreateVideoBody): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.base, body);
  }
  uploadChunk(id: string, index: number, chunk: Blob): Observable<{ received: number }> {
    return this.http.post<{ received: number }>(`${this.base}/${id}/upload/chunk`, chunk, {
      params: new HttpParams().set('index', String(index))
    });
  }
  complete(id: string, total: number): Observable<{ id: string; status: string }> {
    return this.http.post<{ id: string; status: string }>(`${this.base}/${id}/upload/complete`, null, {
      params: new HttpParams().set('total', String(total))
    });
  }
  update(id: string, body: UpdateVideoBody): Observable<VideoDetail> {
    return this.http.put<VideoDetail>(`${this.base}/${id}`, body);
  }
  publish(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/publish`, null);
  }
  archive(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/archive`, null);
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest editor-video.service`
Expected: PASS (6 tests).

- [ ] **Step 5 : Commit**

```bash
git add src/app/core/api/editor-video.service.ts src/app/core/api/editor-video.service.spec.ts
git commit -m "feat(frontend): EditorVideoService (mine/create/upload/complete/update/publish/archive)"
```

---

## Task 9 : Coquille Éditeur (EditorShell)

**Files:**
- Create: `src/app/layouts/editor-shell.component.ts`
- Test: `src/app/layouts/editor-shell.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `editor-shell.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { EditorShellComponent } from './editor-shell.component';

describe('EditorShellComponent', () => {
  beforeEach(() => localStorage.clear());

  it('affiche la barre latérale Studio et un router-outlet', async () => {
    await TestBed.configureTestingModule({
      imports: [EditorShellComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(EditorShellComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Tableau de bord');
    expect(text).toContain('Téléverser');
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest editor-shell`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/layouts/editor-shell.component.ts`**

```typescript
import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../core/auth/auth.store';

@Component({
  selector: 'app-editor-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="min-h-screen flex bg-paper">
      <aside class="w-56 bg-ink text-ink-300 flex flex-col p-3 gap-1">
        <a routerLink="/" class="flex items-center gap-2 font-extrabold text-white mb-4 px-2">
          <span class="w-2.5 h-2.5 bg-map-red rounded-sm"></span> MAP&nbsp;Studio
        </a>
        <a routerLink="/studio" [routerLinkActiveOptions]="{ exact: true }" routerLinkActive="bg-map-red text-white"
           class="px-3 py-2 rounded-lg">Tableau de bord</a>
        <a routerLink="/studio/upload" routerLinkActive="bg-map-red text-white"
           class="px-3 py-2 rounded-lg">Téléverser</a>
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
export class EditorShellComponent {
  store = inject(AuthStore);
  private router = inject(Router);
  logout(): void { this.store.logout(); this.router.navigate(['/']); }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest editor-shell`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/layouts/editor-shell.component.ts src/app/layouts/editor-shell.component.spec.ts
git commit -m "feat(frontend): coquille Éditeur (barre latérale Studio)"
```

---

## Task 10 : Tableau de bord Éditeur (mes vidéos)

**Files:**
- Create: `src/app/features/editor/editor-dashboard.component.ts`
- Test: `src/app/features/editor/editor-dashboard.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `editor-dashboard.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EditorDashboardComponent } from './editor-dashboard.component';

describe('EditorDashboardComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditorDashboardComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge et affiche mes vidéos avec leur statut', () => {
    const fixture = TestBed.createComponent(EditorDashboardComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/videos/mine?page=1').flush({
      items: [{ id: 'v1', title: 'Mon reportage', slug: 's', categoryName: null, durationSeconds: null, publishedAt: null, status: 'Processing' }],
      total: 1, page: 1, pageSize: 20
    });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Mon reportage');
    expect(text).toContain('En traitement');
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest editor-dashboard`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/editor/editor-dashboard.component.ts`**

```typescript
import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EditorVideoService } from '../../core/api/editor-video.service';
import { VideoListItem } from '../../core/models/video.models';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';
import { SpinnerComponent } from '../../shared/ui/spinner.component';

@Component({
  selector: 'app-editor-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent, SpinnerComponent],
  template: `
    <div class="flex items-center mb-4">
      <h1 class="text-xl font-bold text-ink">Mes vidéos</h1>
      <a routerLink="/studio/upload" class="ms-auto px-3 py-2 rounded-lg bg-map-red text-white text-sm font-semibold">+ Téléverser</a>
    </div>

    <div *ngIf="loading()" class="flex justify-center py-10"><app-spinner></app-spinner></div>

    <table *ngIf="!loading()" class="w-full bg-white border border-line rounded-lg overflow-hidden">
      <thead class="text-left text-xs text-muted border-b border-line">
        <tr><th class="p-3">Titre</th><th class="p-3">Statut</th><th class="p-3"></th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let v of items()" class="border-b border-line last:border-0">
          <td class="p-3 text-ink font-medium">{{ v.title }}</td>
          <td class="p-3"><app-status-badge [status]="v.status"></app-status-badge></td>
          <td class="p-3 text-end"><a [routerLink]="['/studio/videos', v.id, 'edit']" class="text-ink underline text-sm">Éditer</a></td>
        </tr>
        <tr *ngIf="items().length === 0"><td colspan="3" class="p-6 text-center text-muted">Aucune vidéo pour l’instant.</td></tr>
      </tbody>
    </table>
  `
})
export class EditorDashboardComponent implements OnInit {
  private api = inject(EditorVideoService);
  items = signal<VideoListItem[]>([]);
  loading = signal(true);

  ngOnInit(): void {
    this.api.mine(1).subscribe((res) => { this.items.set(res.items); this.loading.set(false); });
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest editor-dashboard`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/features/editor/editor-dashboard.component.ts src/app/features/editor/editor-dashboard.component.spec.ts
git commit -m "feat(frontend): tableau de bord éditeur (mes vidéos + statuts)"
```

---

## Task 11 : Upload chunké

**Files:**
- Create: `src/app/features/editor/video-upload.component.ts`
- Test: `src/app/features/editor/video-upload.component.spec.ts`

**Contexte :** l'upload se fait en 3 temps : `create` (brouillon → `{id}`), une boucle `uploadChunk(id, index, blob)` sur les tranches du fichier (`File.slice`), puis `complete(id, totalChunks)`. Taille de tranche : 5 Mo.

- [ ] **Step 1 : Écrire le test qui échoue `video-upload.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { VideoUploadComponent } from './video-upload.component';
import { EditorVideoService } from '../../core/api/editor-video.service';

describe('VideoUploadComponent', () => {
  const router = { navigate: jest.fn() };
  const api = {
    create: jest.fn(() => of({ id: 'v1' })),
    uploadChunk: jest.fn(() => of({ received: 1 })),
    complete: jest.fn(() => of({ id: 'v1', status: 'Processing' }))
  };
  beforeEach(async () => {
    router.navigate.mockClear();
    Object.values(api).forEach((m: any) => m.mockClear());
    await TestBed.configureTestingModule({
      imports: [VideoUploadComponent],
      providers: [provideRouter([]), { provide: Router, useValue: router }, { provide: EditorVideoService, useValue: api }]
    }).compileComponents();
  });

  it('crée, téléverse les chunks puis finalise, et redirige', async () => {
    const fixture = TestBed.createComponent(VideoUploadComponent);
    const cmp = fixture.componentInstance;
    cmp.title = 'Mon sujet';
    // fichier ~12 Mo → 3 chunks de 5 Mo
    cmp.file = new File([new Uint8Array(12 * 1024 * 1024)], 'v.mp4', { type: 'video/mp4' });
    await cmp.submit();
    expect(api.create).toHaveBeenCalledWith({ title: 'Mon sujet', description: undefined, categoryId: undefined });
    expect(api.uploadChunk).toHaveBeenCalledTimes(3);
    expect(api.complete).toHaveBeenCalledWith('v1', 3);
    expect(router.navigate).toHaveBeenCalledWith(['/studio/videos', 'v1', 'edit']);
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest video-upload`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/editor/video-upload.component.ts`**

```typescript
import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { EditorVideoService } from '../../core/api/editor-video.service';

const CHUNK_SIZE = 5 * 1024 * 1024;

@Component({
  selector: 'app-video-upload',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">Téléverser une vidéo</h1>
    <div class="max-w-lg space-y-3 bg-white border border-line rounded-lg p-5">
      <input [(ngModel)]="title" name="title" required placeholder="Titre"
             class="w-full px-3 py-2 rounded-lg border border-line" />
      <textarea [(ngModel)]="description" name="description" placeholder="Description"
                class="w-full px-3 py-2 rounded-lg border border-line"></textarea>
      <input type="file" accept="video/*" (change)="onFile($event)" />
      <div *ngIf="uploading()" class="h-2 bg-line rounded-full overflow-hidden">
        <div class="h-full bg-ink" [style.width.%]="progress()"></div>
      </div>
      <p *ngIf="error()" class="text-sm text-map-red">{{ error() }}</p>
      <button (click)="submit()" [disabled]="uploading() || !file || !title"
              class="px-4 py-2 rounded-lg bg-ink text-white font-semibold disabled:opacity-50">Lancer le téléversement</button>
    </div>
  `
})
export class VideoUploadComponent {
  private api = inject(EditorVideoService);
  private router = inject(Router);
  title = '';
  description = '';
  file: File | null = null;
  uploading = signal(false);
  progress = signal(0);
  error = signal<string | null>(null);

  onFile(e: Event): void {
    this.file = (e.target as HTMLInputElement).files?.[0] ?? null;
  }

  async submit(): Promise<void> {
    if (!this.file || !this.title) return;
    this.uploading.set(true);
    this.error.set(null);
    try {
      const { id } = await firstValueFrom(
        this.api.create({ title: this.title, description: this.description || undefined, categoryId: undefined })
      );
      const total = Math.ceil(this.file.size / CHUNK_SIZE);
      for (let i = 0; i < total; i++) {
        const slice = this.file.slice(i * CHUNK_SIZE, (i + 1) * CHUNK_SIZE);
        await firstValueFrom(this.api.uploadChunk(id, i, slice));
        this.progress.set(Math.round(((i + 1) / total) * 100));
      }
      await firstValueFrom(this.api.complete(id, total));
      this.router.navigate(['/studio/videos', id, 'edit']);
    } catch {
      this.error.set('Échec du téléversement.');
    } finally {
      this.uploading.set(false);
    }
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest video-upload`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/features/editor/video-upload.component.ts src/app/features/editor/video-upload.component.spec.ts
git commit -m "feat(frontend): upload chunké de vidéo (create → chunks → complete)"
```

---

## Task 12 : Édition de métadonnées + publish/archive

**Files:**
- Create: `src/app/features/editor/video-edit.component.ts`
- Test: `src/app/features/editor/video-edit.component.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `video-edit.component.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { VideoEditComponent } from './video-edit.component';

describe('VideoEditComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VideoEditComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge le détail puis enregistre les métadonnées', () => {
    const fixture = TestBed.createComponent(VideoEditComponent);
    fixture.componentRef.setInput('id', 'v1');
    fixture.detectChanges();

    http.expectOne('/api/v1/videos/v1').flush({
      id: 'v1', title: 'Titre', description: 'D', slug: 's', status: 'Ready',
      categoryName: null, durationSeconds: null, publishedAt: null, tags: ['a', 'b'], renditions: []
    });
    http.expectOne('/api/v1/categories').flush([]);
    fixture.detectChanges();

    fixture.componentInstance.save();
    const req = http.expectOne('/api/v1/videos/v1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.tags).toEqual(['a', 'b']);
    req.flush({});
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest video-edit`
Expected: FAIL — module introuvable.

- [ ] **Step 3 : Créer `src/app/features/editor/video-edit.component.ts`**

```typescript
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CatalogService } from '../../core/api/catalog.service';
import { CategoryService } from '../../core/api/category.service';
import { EditorVideoService } from '../../core/api/editor-video.service';
import { Category } from '../../core/models/video.models';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';

@Component({
  selector: 'app-video-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, StatusBadgeComponent],
  template: `
    <div class="max-w-lg space-y-3">
      <div class="flex items-center gap-3">
        <h1 class="text-xl font-bold text-ink">Éditer la vidéo</h1>
        <app-status-badge [status]="status()"></app-status-badge>
      </div>
      <input [(ngModel)]="title" name="title" placeholder="Titre" class="w-full px-3 py-2 rounded-lg border border-line" />
      <textarea [(ngModel)]="description" name="description" placeholder="Description" class="w-full px-3 py-2 rounded-lg border border-line"></textarea>
      <select [(ngModel)]="categoryId" name="cat" class="w-full px-3 py-2 rounded-lg border border-line">
        <option [ngValue]="null">— Sans catégorie —</option>
        <option *ngFor="let c of categories()" [ngValue]="c.id">{{ c.name }}</option>
      </select>
      <input [(ngModel)]="tagsCsv" name="tags" placeholder="Tags (séparés par des virgules)" class="w-full px-3 py-2 rounded-lg border border-line" />
      <div class="flex gap-2">
        <button (click)="save()" class="px-4 py-2 rounded-lg bg-ink text-white font-semibold">Enregistrer</button>
        <button (click)="publish()" class="px-4 py-2 rounded-lg bg-emerald-600 text-white font-semibold">Publier</button>
        <button (click)="archive()" class="px-4 py-2 rounded-lg border border-line">Archiver</button>
      </div>
      <p *ngIf="message()" class="text-sm text-emerald-700">{{ message() }}</p>
    </div>
  `
})
export class VideoEditComponent implements OnInit {
  @Input({ required: true }) id!: string;
  private catalog = inject(CatalogService);
  private categoryApi = inject(CategoryService);
  private api = inject(EditorVideoService);
  private router = inject(Router);

  title = '';
  description = '';
  categoryId: string | null = null;
  tagsCsv = '';
  status = signal('Draft');
  categories = signal<Category[]>([]);
  message = signal<string | null>(null);

  ngOnInit(): void {
    this.categoryApi.list().subscribe((cs) => this.categories.set(cs));
    this.catalog.get(this.id).subscribe((v) => {
      this.title = v.title;
      this.description = v.description ?? '';
      this.tagsCsv = v.tags.join(', ');
      this.status.set(v.status);
    });
  }

  private tags(): string[] {
    return this.tagsCsv.split(',').map((t) => t.trim()).filter((t) => t.length > 0);
  }

  save(): void {
    this.api.update(this.id, {
      title: this.title, description: this.description || null, categoryId: this.categoryId, tags: this.tags()
    }).subscribe((v) => { this.status.set(v.status); this.message.set('Métadonnées enregistrées.'); });
  }
  publish(): void {
    this.api.publish(this.id).subscribe(() => { this.status.set('Published'); this.message.set('Vidéo publiée.'); });
  }
  archive(): void {
    this.api.archive(this.id).subscribe(() => { this.status.set('Archived'); this.message.set('Vidéo archivée.'); });
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest video-edit`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/features/editor/video-edit.component.ts src/app/features/editor/video-edit.component.spec.ts
git commit -m "feat(frontend): édition métadonnées + publication/archivage"
```

---

## Task 13 : Suite verte + build de production

**Files:** aucun nouveau ; vérification d'intégration.

- [ ] **Step 1 : Lancer toute la suite Jest**

Run: `node_modules/.bin/jest`
Expected: PASS — tous les specs verts (Phase 1 + Phase 2).

- [ ] **Step 2 : Build de production**

Run: `npm run build`
Expected: build réussit, zéro erreur TypeScript (strict). Les chunks lazy `auth`, `editor-shell`, `editor-dashboard`, `video-upload`, `video-edit` apparaissent.

- [ ] **Step 3 : Commit (si ajustements)**

```bash
git add -A frontend
git commit -m "chore(frontend): Phase 2 verte (suite Jest + build prod)"
```

---

## Task 14 : Smoke e2e Cypress (parcours éditeur)

**Files:**
- Create: `frontend/cypress/e2e/editor-flow.cy.ts`

**Note :** le binaire Cypress est indisponible dans cet environnement (voir mémoire projet `frontend-toolchain-gotchas`). Écrire et committer le spec ; l'exécution réelle est différée.

- [ ] **Step 1 : Créer `cypress/e2e/editor-flow.cy.ts`**

```typescript
describe('Parcours éditeur', () => {
  it('se connecte et voit son tableau de bord', () => {
    cy.intercept('POST', '/api/v1/auth/login', {
      token: 't', expiresAt: '2030-01-01T00:00:00Z', userId: 'u', email: 'ed@map.ma', displayName: 'Éditeur', roles: ['Editeur']
    });
    cy.intercept('GET', '/api/v1/videos/mine*', { items: [], total: 0, page: 1, pageSize: 20 });

    cy.visit('/auth/login');
    cy.get('input[name=email]').type('ed@map.ma');
    cy.get('input[name=password]').type('p');
    cy.contains('Se connecter').click();
    cy.url().should('include', '/studio');
    cy.contains('Mes vidéos');
  });
});
```

- [ ] **Step 2 : Commit**

```bash
git add frontend/cypress/e2e/editor-flow.cy.ts
git commit -m "test(frontend): smoke e2e Cypress du parcours éditeur (login → dashboard)"
```

---

## Auto-revue (couverture vs spec Phase 2)

- **AuthStore signals + persistance (§4-5)** → Tasks 1-2. ✓
- **Intercepteur Bearer + 401 (§5)** → Task 3. ✓
- **Gardes authGuard/editorGuard (§5)** → Task 4. ✓
- **Login + Register, auto-inscription Visiteur (§5-6)** → Tasks 5-6. ✓
- **Routes /auth, /studio + Connexion/Déconnexion (§3)** → Task 7. ✓
- **EditorVideoService (§6 Éditeur : mine/upload/PUT/publish/archive)** → Task 8. ✓
- **Coquille Éditeur sidebar (§3)** → Task 9. ✓
- **Tableau de bord « mes vidéos » + statuts (§6)** → Task 10. ✓
- **Upload chunké + suivi (§6, §7)** → Task 11 (barre de progression ; le suivi de transcodage détaillé s'appuie sur le statut affiché au dashboard). ✓
- **Édition métadonnées + publish/archive (§6)** → Task 12. ✓
- **Tests Jest + smoke Cypress (§8)** → tests par tâche + Tasks 13-14. ✓

**Hors périmètre Phase 2 (plans ultérieurs) :** espace Admin (utilisateurs/stats/audit/config), i18n FR/AR + RTL, gestion d'erreur globale par toasts (un message d'erreur local est inclus dans les formulaires et l'upload). Pas de placeholder ; types cohérents (`AuthResponse`/`AuthUser`/`UpdateVideoBody`/`VideoListItem` réutilisés tels quels).

Le test de garde (Task 4) utilise `TestBed.runInInjectionContext` (Angular 17) pour exécuter les `CanActivateFn` dans un contexte d'injection.
