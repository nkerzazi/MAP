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
