import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { PublicShellComponent } from './public-shell.component';
import { TranslationService } from '../core/i18n/translation.service';

describe('PublicShellComponent', () => {
  beforeEach(() => localStorage.clear());

  it('affiche la marque, Connexion (FR) puis bascule en arabe', async () => {
    await TestBed.configureTestingModule({
      imports: [PublicShellComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(PublicShellComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Connexion');

    TestBed.inject(TranslationService).setLang('ar');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('تسجيل الدخول');
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });
});
