import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { CategoryService } from './category.service';

describe('CategoryService (admin)', () => {
  let service: CategoryService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [CategoryService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(CategoryService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('crée une catégorie', () => {
    service.create('Actualités').subscribe();
    const req = http.expectOne('/api/v1/categories');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Actualités' });
    req.flush({ id: 'c1', name: 'Actualités', slug: 'actualites' });
  });

  it('renomme et supprime', () => {
    service.update('c1', 'Sport').subscribe();
    const put = http.expectOne('/api/v1/categories/c1');
    expect(put.request.method).toBe('PUT');
    put.flush({ id: 'c1', name: 'Sport', slug: 'sport' });
    service.delete('c1').subscribe();
    expect(http.expectOne('/api/v1/categories/c1').request.method).toBe('DELETE');
  });
});
