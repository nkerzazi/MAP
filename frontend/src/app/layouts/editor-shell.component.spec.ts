import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { EditorShellComponent } from './editor-shell.component';

describe('EditorShellComponent', () => {
  beforeEach(() => localStorage.clear());

  it('affiche la barre latérale Studio et un router-outlet', async () => {
    await TestBed.configureTestingModule({
      imports: [EditorShellComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(EditorShellComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Tableau de bord');
    expect(text).toContain('Téléverser');
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });
});
