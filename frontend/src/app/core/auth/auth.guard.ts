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
