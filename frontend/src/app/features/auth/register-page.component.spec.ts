import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { RegisterPageComponent } from './register-page.component';

describe('RegisterPageComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [RegisterPageComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('inscrit puis redirige vers l’accueil', () => {
    const nav = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(RegisterPageComponent);
    const cmp = fixture.componentInstance;
    fixture.detectChanges();
    cmp.email = 'v@map.ma'; cmp.password = 'p'; cmp.displayName = 'Visiteur';
    cmp.submit();
    http.expectOne('/api/v1/auth/register').flush({
      token: 't', expiresAt: '2030-01-01T00:00:00Z', userId: 'u', email: 'v@map.ma', displayName: 'Visiteur', roles: ['Visiteur']
    });
    expect(nav).toHaveBeenCalledWith(['/']);
  });

  it('affiche une erreur au 409', () => {
    const fixture = TestBed.createComponent(RegisterPageComponent);
    const cmp = fixture.componentInstance;
    fixture.detectChanges();
    cmp.submit();
    http.expectOne('/api/v1/auth/register').flush({ detail: 'Email déjà utilisé.' }, { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    expect(cmp.error()).toContain('Email');
  });
});
