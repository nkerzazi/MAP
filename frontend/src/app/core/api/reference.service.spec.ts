import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { CategoryService } from './category.service';
import { TagService } from './tag.service';
import { AnalyticsService } from './analytics.service';

describe('Services de référence', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('CategoryService liste les catégories', () => {
    TestBed.inject(CategoryService).list().subscribe();
    expect(http.expectOne('/api/v1/categories').request.method).toBe('GET');
  });

  it('TagService liste les tags avec filtre q', () => {
    TestBed.inject(TagService).list('spo').subscribe();
    expect(http.expectOne('/api/v1/tags?q=spo').request.method).toBe('GET');
  });

  it('AnalyticsService envoie la télémétrie de visionnage', () => {
    TestBed.inject(AnalyticsService).recordView('v1', { watchSeconds: 12, sessionId: 's1' }).subscribe();
    const req = http.expectOne('/api/v1/videos/v1/views');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ watchSeconds: 12, sessionId: 's1' });
    req.flush(null);
  });
});
