import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminAuditComponent } from './admin-audit.component';

describe('AdminAuditComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminAuditComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge et affiche le journal d’audit', () => {
    const fixture = TestBed.createComponent(AdminAuditComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/audit?page=1').flush({
      items: [{ id: 'a1', actorId: 'u1', action: 'category.create', entityType: 'Category', entityId: 'c1', occurredAt: '2026-06-22T10:00:00Z' }],
      total: 1, page: 1, pageSize: 20
    });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('category.create');
  });
});
