import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AuthStore } from '../core/auth/auth.store';

@Component({
  selector: 'app-public-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, CommonModule],
  template: `
    <div class="min-h-screen flex flex-col bg-paper">
      <header class="h-14 bg-ink text-white flex items-center gap-4 px-4">
        <a routerLink="/" class="flex items-center gap-2 font-extrabold">
          <span class="w-2.5 h-2.5 bg-map-red rounded-sm"></span> MAP&nbsp;Vidéo
        </a>
        <div class="ms-auto flex items-center gap-3 text-sm">
          <span class="bg-ink-500 rounded-md px-2 py-1 font-semibold">FR · ع</span>
          <a *ngIf="!store.isAuthenticated()" routerLink="/auth/login" class="bg-map-red rounded-lg px-3 py-1.5 font-semibold">Connexion</a>
          <a *ngIf="store.isAuthenticated() && (store.hasRole('Editeur') || store.hasRole('Admin'))" routerLink="/studio" class="underline">Studio</a>
          <a *ngIf="store.isAuthenticated() && store.hasRole('Admin')" routerLink="/admin" class="underline">Admin</a>
          <button *ngIf="store.isAuthenticated()" (click)="store.logout()" class="underline">Déconnexion</button>
        </div>
      </header>
      <main class="flex-1 max-w-6xl w-full mx-auto px-4 py-6">
        <router-outlet></router-outlet>
      </main>
      <footer class="text-center text-xs text-muted py-4">Maghreb Arabe Presse — plateforme vidéo interne</footer>
    </div>
  `
})
export class PublicShellComponent {
  store = inject(AuthStore);
}
