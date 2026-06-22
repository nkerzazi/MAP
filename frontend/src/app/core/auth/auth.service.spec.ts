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
