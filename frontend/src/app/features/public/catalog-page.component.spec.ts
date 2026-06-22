import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { CatalogPageComponent } from './catalog-page.component';

describe('CatalogPageComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CatalogPageComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge catalogue + catégories au démarrage et affiche les vignettes', () => {
    const fixture = TestBed.createComponent(CatalogPageComponent);
    fixture.detectChanges();

    http.expectOne('/api/v1/categories').flush([{ id: 'c1', name: 'Actualités', slug: 'actualites' }]);
    http.expectOne((r) => r.url === '/api/v1/videos').flush({
      items: [{ id: 'v1', title: 'Sommet', slug: 'sommet', categoryName: 'Actualités', durationSeconds: null, publishedAt: null, status: 'Published' }],
      total: 1, page: 1, pageSize: 20
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sommet');
  });

  it('relance la recherche quand un terme est soumis', () => {
    const fixture = TestBed.createComponent(CatalogPageComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/categories').flush([]);
    http.expectOne((r) => r.url === '/api/v1/videos').flush({ items: [], total: 0, page: 1, pageSize: 20 });

    fixture.componentInstance.onSearch('sommet');
    const req = http.expectOne((r) => r.url === '/api/v1/videos' && r.params.get('q') === 'sommet');
    expect(req.request.params.get('q')).toBe('sommet');
    req.flush({ items: [], total: 0, page: 1, pageSize: 20 });
  });
});
