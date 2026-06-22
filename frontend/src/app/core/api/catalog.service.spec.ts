import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { CatalogService } from './catalog.service';

describe('CatalogService', () => {
  let service: CatalogService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CatalogService, provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CatalogService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('construit la requête catalogue avec q, categoryId, tag et page', () => {
    service.list({ q: 'sommet', categoryId: 'c1', tag: 'sport', page: 2 }).subscribe();
    const req = http.expectOne(
      '/api/v1/videos?q=sommet&categoryId=c1&tag=sport&page=2'
    );
    expect(req.request.method).toBe('GET');
    req.flush({ items: [], total: 0, page: 2, pageSize: 20 });
  });

  it('récupère le détail d’une vidéo', () => {
    service.get('v1').subscribe();
    const req = http.expectOne('/api/v1/videos/v1');
    expect(req.request.method).toBe('GET');
    req.flush({});
  });
});
