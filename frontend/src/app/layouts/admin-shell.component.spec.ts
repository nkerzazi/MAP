import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminShellComponent } from './admin-shell.component';

describe('AdminShellComponent', () => {
  beforeEach(() => localStorage.clear());
  it('affiche la navigation Admin et un router-outlet', async () => {
    await TestBed.configureTestingModule({
      imports: [AdminShellComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(AdminShellComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Utilisateurs');
    expect(text).toContain('Audit');
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });
});
