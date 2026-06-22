import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminUsersComponent } from './admin-users.component';

describe('AdminUsersComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminUsersComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('liste les utilisateurs et attribue un rôle', () => {
    const fixture = TestBed.createComponent(AdminUsersComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/users').flush([
      { id: 'u1', email: 'ed@map.ma', displayName: 'Éditeur', isActive: true, roles: ['Visiteur'] }
    ]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('ed@map.ma');

    fixture.componentInstance.assign('u1', 'Editeur');
    const post = http.expectOne('/api/v1/users/u1/roles');
    expect(post.request.body).toEqual({ role: 'Editeur' });
    post.flush(null);
    http.expectOne('/api/v1/users').flush([]);
  });
});
