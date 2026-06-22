import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminCategoriesComponent } from './admin-categories.component';

describe('AdminCategoriesComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminCategoriesComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('liste puis crée une catégorie', () => {
    const fixture = TestBed.createComponent(AdminCategoriesComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/categories').flush([{ id: 'c1', name: 'Actualités', slug: 'actualites' }]);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Actualités');

    fixture.componentInstance.newName = 'Sport';
    fixture.componentInstance.create();
    const post = http.expectOne('/api/v1/categories');
    expect(post.request.body).toEqual({ name: 'Sport' });
    post.flush({ id: 'c2', name: 'Sport', slug: 'sport' });
    http.expectOne('/api/v1/categories').flush([]);
  });
});
