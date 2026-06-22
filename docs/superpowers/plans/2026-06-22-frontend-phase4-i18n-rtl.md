# Frontend MAP — Phase 4 : i18n FR/AR + RTL — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Internationaliser l'interface en français et arabe avec bascule de langue à chaud et mise en page **RTL** pour l'arabe, sans dépendance externe (i18n maison léger basé Signals).

**Architecture :** `TranslationService` (Signals) détient la langue active (`fr`/`ar`), calcule la direction (`ltr`/`rtl`), expose `t(key)` à partir de dictionnaires FR/AR, persiste le choix et applique `dir`/`lang` sur `<html>`. Un pipe impur `| t` traduit dans les templates. La bascule est câblée dans la barre publique. L'arabe reflète la mise en page grâce aux propriétés logiques Tailwind (`ms-`/`me-`) déjà utilisées + l'attribut `dir`.

**Tech Stack :** Angular 17 standalone, Signals, pipe impur, Tailwind (logical properties), Jest. Aucune nouvelle dépendance npm.

**Spec :** [2026-06-22-frontend-angular-interfaces-design.md](../specs/2026-06-22-frontend-angular-interfaces-design.md) §2 (bilingue FR/AR + RTL).

**Pré-requis :** Phases 1-3 mergées. Branche `feat/frontend-phase4`. Chemins relatifs à `frontend/`. Tests : `node_modules/.bin/jest`.

**Périmètre :** infrastructure i18n + bascule + RTL + dictionnaires FR/AR couvrant la **barre publique** et la **page catalogue** (chaîne représentative). Les écrans restants (éditeur/admin) s'internationalisent ensuite en ajoutant des clés aux mêmes dictionnaires, selon le même motif — explicitement hors de ce premier plan i18n pour rester livrable.

---

## Structure de fichiers (Phase 4)

```
src/app/core/i18n/
  translations.ts          # dictionnaires FR + AR (Record<string,string>)
  translation.service.ts   # Signals : lang, dir, t(), setLang, toggle
  translate.pipe.ts        # pipe impur | t
  translation.service.spec.ts · translate.pipe.spec.ts
src/app/app.component.ts            # MODIFIÉ : applique dir/lang au démarrage
src/app/layouts/public-shell.component.ts  # MODIFIÉ : bascule FR/AR + clés t
src/app/features/public/catalog-page.component.ts  # MODIFIÉ : clés t
```

---

## Task 1 : TranslationService + dictionnaires

**Files:**
- Create: `src/app/core/i18n/translations.ts`, `src/app/core/i18n/translation.service.ts`
- Test: `src/app/core/i18n/translation.service.spec.ts`

- [ ] **Step 1 : Créer `src/app/core/i18n/translations.ts`**

```typescript
export type Lang = 'fr' | 'ar';

export const TRANSLATIONS: Record<Lang, Record<string, string>> = {
  fr: {
    'brand.video': 'MAP Vidéo',
    'nav.login': 'Connexion',
    'nav.logout': 'Déconnexion',
    'nav.studio': 'Studio',
    'nav.admin': 'Admin',
    'footer.tagline': 'Maghreb Arabe Presse — plateforme vidéo interne',
    'catalog.title': 'Catalogue',
    'catalog.search': 'Rechercher une vidéo…',
    'catalog.searchBtn': 'Rechercher',
    'catalog.all': 'Toutes',
    'catalog.empty': 'Aucune vidéo trouvée.'
  },
  ar: {
    'brand.video': 'فيديو وم ع',
    'nav.login': 'تسجيل الدخول',
    'nav.logout': 'تسجيل الخروج',
    'nav.studio': 'الاستوديو',
    'nav.admin': 'الإدارة',
    'footer.tagline': 'وكالة المغرب العربي للأنباء — منصة الفيديو الداخلية',
    'catalog.title': 'الفهرس',
    'catalog.search': 'ابحث عن فيديو…',
    'catalog.searchBtn': 'بحث',
    'catalog.all': 'الكل',
    'catalog.empty': 'لا توجد مقاطع فيديو.'
  }
};
```

- [ ] **Step 2 : Écrire le test qui échoue `translation.service.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { TranslationService } from './translation.service';

describe('TranslationService', () => {
  let svc: TranslationService;
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.dir = 'ltr';
    TestBed.configureTestingModule({ providers: [TranslationService] });
    svc = TestBed.inject(TranslationService);
  });

  it('traduit en français par défaut', () => {
    expect(svc.lang()).toBe('fr');
    expect(svc.t('nav.login')).toBe('Connexion');
    expect(svc.dir()).toBe('ltr');
  });

  it('bascule en arabe : traduction, direction RTL et persistance', () => {
    svc.setLang('ar');
    expect(svc.t('nav.login')).toBe('تسجيل الدخول');
    expect(svc.dir()).toBe('rtl');
    expect(document.documentElement.dir).toBe('rtl');
    expect(localStorage.getItem('map_lang')).toBe('ar');
  });

  it('toggle alterne fr/ar', () => {
    svc.toggle();
    expect(svc.lang()).toBe('ar');
    svc.toggle();
    expect(svc.lang()).toBe('fr');
  });

  it('renvoie la clé si traduction absente', () => {
    expect(svc.t('clé.inconnue')).toBe('clé.inconnue');
  });
});
```

- [ ] **Step 3 : Lancer → échec attendu**

Run: `node_modules/.bin/jest translation.service`
Expected: FAIL — `Cannot find module './translation.service'`.

- [ ] **Step 4 : Créer `src/app/core/i18n/translation.service.ts`**

```typescript
import { Injectable, computed, signal } from '@angular/core';
import { Lang, TRANSLATIONS } from './translations';

const LANG_KEY = 'map_lang';

@Injectable({ providedIn: 'root' })
export class TranslationService {
  private _lang = signal<Lang>(this.initial());
  lang = this._lang.asReadonly();
  dir = computed<'ltr' | 'rtl'>(() => (this._lang() === 'ar' ? 'rtl' : 'ltr'));

  constructor() { this.apply(this._lang()); }

  t(key: string): string {
    return TRANSLATIONS[this._lang()][key] ?? key;
  }
  setLang(lang: Lang): void {
    this._lang.set(lang);
    localStorage.setItem(LANG_KEY, lang);
    this.apply(lang);
  }
  toggle(): void {
    this.setLang(this._lang() === 'fr' ? 'ar' : 'fr');
  }

  private initial(): Lang {
    return localStorage.getItem(LANG_KEY) === 'ar' ? 'ar' : 'fr';
  }
  private apply(lang: Lang): void {
    if (typeof document !== 'undefined') {
      document.documentElement.dir = lang === 'ar' ? 'rtl' : 'ltr';
      document.documentElement.lang = lang;
    }
  }
}
```

- [ ] **Step 5 : Lancer → succès**

Run: `node_modules/.bin/jest translation.service`
Expected: PASS (4 tests).

- [ ] **Step 6 : Commit**

```bash
git add src/app/core/i18n/translations.ts src/app/core/i18n/translation.service.ts src/app/core/i18n/translation.service.spec.ts
git commit -m "feat(frontend): TranslationService i18n FR/AR (signals, RTL, persistance)"
```

---

## Task 2 : Pipe `t`

**Files:**
- Create: `src/app/core/i18n/translate.pipe.ts`
- Test: `src/app/core/i18n/translate.pipe.spec.ts`

- [ ] **Step 1 : Écrire le test qui échoue `translate.pipe.spec.ts`**

```typescript
import { TestBed } from '@angular/core/testing';
import { TranslatePipe } from './translate.pipe';
import { TranslationService } from './translation.service';

describe('TranslatePipe', () => {
  let pipe: TranslatePipe;
  let svc: TranslationService;
  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({ providers: [TranslationService] });
    svc = TestBed.inject(TranslationService);
    pipe = TestBed.runInInjectionContext(() => new TranslatePipe());
  });

  it('traduit la clé selon la langue active', () => {
    expect(pipe.transform('nav.login')).toBe('Connexion');
    svc.setLang('ar');
    expect(pipe.transform('nav.login')).toBe('تسجيل الدخول');
  });
});
```

- [ ] **Step 2 : Lancer → échec attendu**

Run: `node_modules/.bin/jest translate.pipe`
Expected: FAIL — `Cannot find module './translate.pipe'`.

- [ ] **Step 3 : Créer `src/app/core/i18n/translate.pipe.ts`**

```typescript
import { Pipe, PipeTransform, inject } from '@angular/core';
import { TranslationService } from './translation.service';

@Pipe({ name: 't', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  private ts = inject(TranslationService);
  transform(key: string): string {
    return this.ts.t(key);
  }
}
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest translate.pipe`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/core/i18n/translate.pipe.ts src/app/core/i18n/translate.pipe.spec.ts
git commit -m "feat(frontend): pipe impur | t pour la traduction dans les templates"
```

---

## Task 3 : Bascule FR/AR dans la barre publique + RTL au démarrage

**Files:**
- Modify: `src/app/app.component.ts`, `src/app/layouts/public-shell.component.ts`
- Test: `src/app/layouts/public-shell.component.spec.ts` (mise à jour)

- [ ] **Step 1 : Modifier `src/app/app.component.ts`** pour initialiser la direction au démarrage

```typescript
import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TranslationService } from './core/i18n/translation.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet],
  template: `<router-outlet></router-outlet>`
})
export class AppComponent {
  // L'injection applique la langue/direction persistée via le constructeur du service.
  private i18n = inject(TranslationService);
}
```

- [ ] **Step 2 : Modifier `src/app/layouts/public-shell.component.ts`** — bascule + clés `t`

```typescript
import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AuthStore } from '../core/auth/auth.store';
import { TranslationService } from '../core/i18n/translation.service';
import { TranslatePipe } from '../core/i18n/translate.pipe';

@Component({
  selector: 'app-public-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, CommonModule, TranslatePipe],
  template: `
    <div class="min-h-screen flex flex-col bg-paper">
      <header class="h-14 bg-ink text-white flex items-center gap-4 px-4">
        <a routerLink="/" class="flex items-center gap-2 font-extrabold">
          <span class="w-2.5 h-2.5 bg-map-red rounded-sm"></span> {{ 'brand.video' | t }}
        </a>
        <div class="ms-auto flex items-center gap-3 text-sm">
          <button (click)="i18n.toggle()" class="bg-ink-500 rounded-md px-2 py-1 font-semibold">
            <span [class.text-white]="i18n.lang() === 'fr'">FR</span> ·
            <span [class.text-white]="i18n.lang() === 'ar'">ع</span>
          </button>
          <a *ngIf="!store.isAuthenticated()" routerLink="/auth/login" class="bg-map-red rounded-lg px-3 py-1.5 font-semibold">{{ 'nav.login' | t }}</a>
          <a *ngIf="store.isAuthenticated() && (store.hasRole('Editeur') || store.hasRole('Admin'))" routerLink="/studio" class="underline">{{ 'nav.studio' | t }}</a>
          <a *ngIf="store.isAuthenticated() && store.hasRole('Admin')" routerLink="/admin" class="underline">{{ 'nav.admin' | t }}</a>
          <button *ngIf="store.isAuthenticated()" (click)="store.logout()" class="underline">{{ 'nav.logout' | t }}</button>
        </div>
      </header>
      <main class="flex-1 max-w-6xl w-full mx-auto px-4 py-6">
        <router-outlet></router-outlet>
      </main>
      <footer class="text-center text-xs text-muted py-4">{{ 'footer.tagline' | t }}</footer>
    </div>
  `
})
export class PublicShellComponent {
  store = inject(AuthStore);
  i18n = inject(TranslationService);
}
```

- [ ] **Step 3 : Mettre à jour `public-shell.component.spec.ts`** — vérifier la traduction de la bascule

```typescript
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { PublicShellComponent } from './public-shell.component';
import { TranslationService } from '../core/i18n/translation.service';

describe('PublicShellComponent', () => {
  beforeEach(() => localStorage.clear());

  it('affiche la marque, Connexion (FR) puis bascule en arabe', async () => {
    await TestBed.configureTestingModule({
      imports: [PublicShellComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(PublicShellComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Connexion');

    TestBed.inject(TranslationService).setLang('ar');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('تسجيل الدخول');
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });
});
```

- [ ] **Step 4 : Lancer → succès**

Run: `node_modules/.bin/jest public-shell`
Expected: PASS.

- [ ] **Step 5 : Commit**

```bash
git add src/app/app.component.ts src/app/layouts/public-shell.component.ts src/app/layouts/public-shell.component.spec.ts
git commit -m "feat(frontend): bascule FR/AR + RTL dans la barre publique"
```

---

## Task 4 : Internationaliser la page catalogue

**Files:**
- Modify: `src/app/features/public/catalog-page.component.ts`
- Test: `src/app/features/public/catalog-page.component.spec.ts` (mise à jour mineure)

- [ ] **Step 1 : Modifier `catalog-page.component.ts`** — importer le pipe et remplacer les chaînes par des clés

Ajouter `TranslatePipe` aux imports et au tableau `imports`, puis dans le template :
- `<h1 ...>Catalogue</h1>` → `<h1 ...>{{ 'catalog.title' | t }}</h1>`
- le bouton `Toutes` → `{{ 'catalog.all' | t }}`
- le `<p ...>Aucune vidéo trouvée.</p>` → `{{ 'catalog.empty' | t }}`

Bloc d'imports mis à jour :

```typescript
import { TranslatePipe } from '../../core/i18n/translate.pipe';
// ...
@Component({
  selector: 'app-catalog-page',
  standalone: true,
  imports: [CommonModule, VideoCardComponent, SearchBarComponent, PaginationComponent, SpinnerComponent, TranslatePipe],
  // template : remplacer les 3 chaînes ci-dessus par les clés | t
```

Les 3 lignes de template concernées deviennent :

```html
        <h1 class="text-xl font-bold text-ink">{{ 'catalog.title' | t }}</h1>
```
```html
        <button (click)="filterCategory(null)"
                class="px-3 py-1 rounded-full text-sm border"
                [class]="!categoryId() ? 'bg-ink text-white border-ink' : 'border-line text-ink'">{{ 'catalog.all' | t }}</button>
```
```html
        <p *ngIf="items().length === 0" class="text-muted py-10 text-center">{{ 'catalog.empty' | t }}</p>
```

- [ ] **Step 2 : Mettre à jour `catalog-page.component.spec.ts`** — le 1er test vérifie l'affichage d'une vignette ; il reste vert (le titre traduit « Catalogue » s'affiche toujours en FR par défaut). Aucune modification nécessaire si les tests existants n'asserent pas sur des chaînes retirées. Vérifier en lançant la suite (Step 3). Si un test échoue sur une chaîne, l'adapter en gardant la langue FR par défaut (les clés rendent « Catalogue », « Toutes », « Aucune vidéo trouvée. » à l'identique).

- [ ] **Step 3 : Lancer → succès**

Run: `node_modules/.bin/jest catalog-page`
Expected: PASS (2 tests ; le rendu FR par défaut est inchangé).

- [ ] **Step 4 : Commit**

```bash
git add src/app/features/public/catalog-page.component.ts src/app/features/public/catalog-page.component.spec.ts
git commit -m "feat(frontend): page catalogue internationalisée (clés FR/AR)"
```

---

## Task 5 : Suite verte + build + smoke i18n

**Files:**
- Create: `frontend/cypress/e2e/i18n.cy.ts`
- Vérification : suite Jest + build.

- [ ] **Step 1 : Lancer toute la suite Jest**

Run: `node_modules/.bin/jest`
Expected: PASS — tous les specs verts (Phases 1-4).

- [ ] **Step 2 : Build de production**

Run: `npm run build`
Expected: build réussit, zéro erreur TypeScript.

- [ ] **Step 3 : Créer `cypress/e2e/i18n.cy.ts`** (run différé — binaire Cypress indisponible)

```typescript
describe('Bascule de langue', () => {
  it('passe l’interface en arabe et en RTL', () => {
    cy.intercept('GET', '/api/v1/categories', []);
    cy.intercept('GET', '/api/v1/videos*', { items: [], total: 0, page: 1, pageSize: 20 });
    cy.visit('/');
    cy.contains('Connexion');
    cy.get('header button').first().click(); // bascule FR/AR
    cy.contains('تسجيل الدخول');
    cy.get('html').should('have.attr', 'dir', 'rtl');
  });
});
```

- [ ] **Step 4 : Commit**

```bash
git add frontend/cypress/e2e/i18n.cy.ts
git commit -m "test(frontend): smoke e2e Cypress bascule FR/AR + RTL"
```

---

## Auto-revue (couverture vs spec Phase 4)

- **i18n FR/AR avec bascule à chaud (§2)** → Tasks 1-3 (TranslationService signals + pipe + bouton de bascule). ✓
- **Mise en page RTL pour l'arabe (§2)** → Task 1 (`dir` sur `<html>`) + classes logiques `ms-`/`me-` déjà en place dans les coquilles. ✓
- **Persistance du choix de langue** → Task 1 (`localStorage`). ✓
- **Application aux écrans** → barre publique (Task 3) + catalogue (Task 4) comme chaîne représentative. ✓
- **Tests Jest + smoke Cypress** → tests par tâche + Task 5. ✓

**Hors périmètre de ce plan i18n :** traduction des écrans éditeur/admin et auth — s'ajoutent en étendant `TRANSLATIONS` (FR/AR) et en remplaçant les chaînes par `| t`, selon le motif des Tasks 3-4, sans changement d'architecture. Aucune dépendance externe ajoutée (contrainte d'auto-hébergement respectée). Pas de placeholder ; types cohérents (`Lang`, `TranslationService` réutilisés).
