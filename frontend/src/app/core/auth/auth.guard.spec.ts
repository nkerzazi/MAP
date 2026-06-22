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
