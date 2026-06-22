import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { EditorDashboardComponent } from './editor-dashboard.component';

describe('EditorDashboardComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [EditorDashboardComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge et affiche mes vidéos avec leur statut', () => {
    const fixture = TestBed.createComponent(EditorDashboardComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/videos/mine?page=1').flush({
      items: [{ id: 'v1', title: 'Mon reportage', slug: 's', categoryName: null, durationSeconds: null, publishedAt: null, status: 'Processing' }],
      total: 1, page: 1, pageSize: 20
    });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Mon reportage');
    expect(text).toContain('En traitement');
  });

  it('en cas d’erreur API : arrête le spinner et affiche une erreur', () => {
    const fixture = TestBed.createComponent(EditorDashboardComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/videos/mine?page=1').flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(fixture.componentInstance.loading()).toBe(false);
    expect(fixture.componentInstance.error()).toBe(true);
  });
});
