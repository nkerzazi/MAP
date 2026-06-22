import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { EditorVideoService } from './editor-video.service';

describe('EditorVideoService', () => {
  let service: EditorVideoService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [EditorVideoService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(EditorVideoService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('liste mes vidéos paginées', () => {
    service.mine(2).subscribe();
    expect(http.expectOne('/api/v1/videos/mine?page=2').request.method).toBe('GET');
  });

  it('crée un brouillon', () => {
    service.create({ title: 'T', description: 'D', categoryId: 'c1' }).subscribe();
    const req = http.expectOne('/api/v1/videos');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ title: 'T', description: 'D', categoryId: 'c1' });
    req.flush({ id: 'v1' });
  });

  it('téléverse un chunk avec index', () => {
    const blob = new Blob([new Uint8Array([1, 2, 3])]);
    service.uploadChunk('v1', 0, blob).subscribe();
    const req = http.expectOne('/api/v1/videos/v1/upload/chunk?index=0');
    expect(req.request.method).toBe('POST');
    req.flush({ received: 3 });
  });

  it('finalise l’upload avec total', () => {
    service.complete('v1', 2).subscribe();
    const req = http.expectOne('/api/v1/videos/v1/upload/complete?total=2');
    expect(req.request.method).toBe('POST');
    req.flush({ id: 'v1', status: 'Processing' });
  });

  it('met à jour les métadonnées', () => {
    service.update('v1', { title: 'T2', description: null, categoryId: null, tags: ['x'] }).subscribe();
    const req = http.expectOne('/api/v1/videos/v1');
    expect(req.request.method).toBe('PUT');
    req.flush({});
  });

  it('publie et archive', () => {
    service.publish('v1').subscribe();
    expect(http.expectOne('/api/v1/videos/v1/publish').request.method).toBe('POST');
    service.archive('v1').subscribe();
    expect(http.expectOne('/api/v1/videos/v1/archive').request.method).toBe('POST');
  });
});
