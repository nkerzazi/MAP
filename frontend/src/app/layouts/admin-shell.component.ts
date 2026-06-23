import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../core/auth/auth.store';
import { TranslatePipe } from '../core/i18n/translate.pipe';

@Component({
  selector: 'app-admin-shell',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  template: `
    <div class="min-h-screen flex bg-paper">
      <aside class="w-56 bg-ink text-ink-300 flex flex-col p-3 gap-1">
        <a routerLink="/" class="flex items-center gap-2 font-extrabold text-white mb-4 px-2">
          <img src="assets/map-logo.png" alt="MAP" class="h-8 w-auto rounded bg-white p-0.5" /> {{ 'brand.admin' | t }}
        </a>
        <a routerLink="/admin" [routerLinkActiveOptions]="{ exact: true }" routerLinkActive="bg-map-red text-white" class="px-3 py-2 rounded-lg">{{ 'admin.nav.dashboard' | t }}</a>
        <a routerLink="/admin/users" routerLinkActive="bg-map-red text-white" class="px-3 py-2 rounded-lg">{{ 'admin.nav.users' | t }}</a>
        <a routerLink="/admin/categories" routerLinkActive="bg-map-red text-white" class="px-3 py-2 rounded-lg">{{ 'admin.nav.categories' | t }}</a>
        <a routerLink="/admin/audit" routerLinkActive="bg-map-red text-white" class="px-3 py-2 rounded-lg">{{ 'admin.nav.audit' | t }}</a>
        <a routerLink="/admin/config" routerLinkActive="bg-map-red text-white" class="px-3 py-2 rounded-lg">{{ 'admin.nav.config' | t }}</a>
        <a routerLink="/studio" class="px-3 py-2 rounded-lg mt-4 border-t border-ink-600/40 pt-3">→ {{ 'nav.studio' | t }}</a>
      </aside>
      <div class="flex-1 flex flex-col">
        <header class="h-14 bg-white border-b border-line flex items-center px-4">
          <span class="ms-auto text-sm text-ink">{{ store.user()?.displayName }}</span>
          <button (click)="logout()" class="ms-3 text-sm text-map-red underline">{{ 'nav.logout' | t }}</button>
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
