import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { LoginPageComponent } from './login-page.component';

describe('LoginPageComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [LoginPageComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('connecte puis redirige vers /studio', () => {
    const nav = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(LoginPageComponent);
    const cmp = fixture.componentInstance;
    fixture.detectChanges();
    cmp.email = 'ed@map.ma'; cmp.password = 'p';
    cmp.submit();
    http.expectOne('/api/v1/auth/login').flush({
      token: 't', expiresAt: '2030-01-01T00:00:00Z', userId: 'u', email: 'ed@map.ma', displayName: 'Ed', roles: ['Editeur']
    });
    expect(nav).toHaveBeenCalledWith(['/studio']);
  });

  it('un administrateur est redirigé vers /admin', () => {
    const nav = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(LoginPageComponent);
    const cmp = fixture.componentInstance;
    fixture.detectChanges();
    cmp.email = 'sa'; cmp.password = 'sa';
    cmp.submit();
    http.expectOne('/api/v1/auth/login').flush({
      token: 't', expiresAt: '2030-01-01T00:00:00Z', userId: 'u', email: 'sa', displayName: 'Super administrateur', roles: ['Admin']
    });
    expect(nav).toHaveBeenCalledWith(['/admin']);
  });

  it('affiche une erreur au 401', () => {
    const fixture = TestBed.createComponent(LoginPageComponent);
    const cmp = fixture.componentInstance;
    fixture.detectChanges();
    cmp.submit();
    http.expectOne('/api/v1/auth/login').flush({ detail: 'Identifiants invalides.' }, { status: 401, statusText: 'Unauthorized' });
    fixture.detectChanges();
    expect(cmp.error()).toContain('Identifiants');
  });
});
