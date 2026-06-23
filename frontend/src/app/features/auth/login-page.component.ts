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
      next: () => {
        this.loading.set(false);
        // Un administrateur arrive sur son espace Admin ; un éditeur sur le Studio.
        this.router.navigate([this.store.hasRole('Admin') ? '/admin' : '/studio']);
      },
      error: (e: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(e.error?.detail ?? 'Identifiants invalides.');
      }
    });
  }
}
