import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AdminService } from './admin.service';

describe('AdminService', () => {
  let service: AdminService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [AdminService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AdminService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('récupère les stats', () => {
    service.stats().subscribe();
    expect(http.expectOne('/api/v1/stats').request.method).toBe('GET');
  });

  it('récupère l’audit paginé', () => {
    service.audit(3).subscribe();
    expect(http.expectOne('/api/v1/audit?page=3').request.method).toBe('GET');
  });

  it('lit et écrit la configuration', () => {
    service.config().subscribe();
    expect(http.expectOne('/api/v1/config').request.method).toBe('GET');
    service.setConfig('siteName', 'MAP').subscribe();
    const req = http.expectOne('/api/v1/config');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ key: 'siteName', value: 'MAP' });
    req.flush(null);
  });
});
