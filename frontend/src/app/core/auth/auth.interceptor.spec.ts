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
    router.navigate.mockClear();
  });
  afterEach(() => http.verify());

  it('ajoute l’en-tête Authorization quand un token est présent', () => {
    store['_token'].set('jwt-xyz');
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
