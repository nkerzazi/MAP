import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { PublicShellComponent } from './public-shell.component';

describe('PublicShellComponent', () => {
  beforeEach(() => localStorage.clear());

  it('affiche la barre MAP, un lien Connexion (anonyme) et un router-outlet', async () => {
    await TestBed.configureTestingModule({
      imports: [PublicShellComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(PublicShellComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('MAP');
    expect(text).toContain('Connexion');
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });
});
