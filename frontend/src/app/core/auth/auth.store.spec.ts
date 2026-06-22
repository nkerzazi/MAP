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
