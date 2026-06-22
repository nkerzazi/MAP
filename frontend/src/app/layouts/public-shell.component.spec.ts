import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { PublicShellComponent } from './public-shell.component';

describe('PublicShellComponent', () => {
  it('affiche la barre de marque MAP et un router-outlet', async () => {
    await TestBed.configureTestingModule({
      imports: [PublicShellComponent],
      providers: [provideRouter([])]
    }).compileComponents();
    const fixture = TestBed.createComponent(PublicShellComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('MAP');
    expect(fixture.nativeElement.querySelector('router-outlet')).toBeTruthy();
  });
});
