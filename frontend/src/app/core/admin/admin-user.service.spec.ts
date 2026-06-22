import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AdminUserService } from './admin-user.service';

describe('AdminUserService', () => {
  let service: AdminUserService;
  let http: HttpTestingController;
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [AdminUserService, provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AdminUserService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('liste les utilisateurs', () => {
    service.list().subscribe();
    expect(http.expectOne('/api/v1/users').request.method).toBe('GET');
  });

  it('met à jour un utilisateur', () => {
    service.update('u1', { isActive: false, displayName: 'X' }).subscribe();
    const req = http.expectOne('/api/v1/users/u1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ isActive: false, displayName: 'X' });
    req.flush(null);
  });

  it('attribue et retire un rôle', () => {
    service.assignRole('u1', 'Editeur').subscribe();
    const a = http.expectOne('/api/v1/users/u1/roles');
    expect(a.request.method).toBe('POST');
    expect(a.request.body).toEqual({ role: 'Editeur' });
    a.flush(null);
    service.removeRole('u1', 'Editeur').subscribe();
    expect(http.expectOne('/api/v1/users/u1/roles/Editeur').request.method).toBe('DELETE');
  });
});
