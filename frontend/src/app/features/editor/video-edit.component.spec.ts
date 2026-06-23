import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { VideoEditComponent } from './video-edit.component';

describe('VideoEditComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VideoEditComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge le détail puis enregistre les métadonnées', () => {
    const fixture = TestBed.createComponent(VideoEditComponent);
    fixture.componentRef.setInput('id', 'v1');
    fixture.detectChanges();

    http.expectOne('/api/v1/videos/v1').flush({
      id: 'v1', title: 'Titre', description: 'D', slug: 's', status: 'Ready',
      categoryName: null, durationSeconds: null, publishedAt: null, tags: ['a', 'b'], renditions: []
    });
    http.expectOne('/api/v1/categories').flush([]);
    fixture.detectChanges();

    fixture.componentInstance.save();
    const req = http.expectOne('/api/v1/videos/v1');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body.tags).toEqual(['a', 'b']);
    req.flush({});
  });

  function load() {
    const fixture = TestBed.createComponent(VideoEditComponent);
    fixture.componentRef.setInput('id', 'v1');
    fixture.detectChanges();
    http.expectOne('/api/v1/videos/v1').flush({
      id: 'v1', title: 'T', description: null, slug: 's', status: 'Ready',
      categoryName: null, durationSeconds: null, publishedAt: null, tags: [], renditions: []
    });
    http.expectOne('/api/v1/categories').flush([]);
    fixture.detectChanges();
    return fixture;
  }

  it('publier (succès) : notification OK affichée', () => {
    const fixture = load();
    fixture.componentInstance.publish();
    http.expectOne('/api/v1/videos/v1/publish').flush(null);
    fixture.detectChanges();
    expect(fixture.componentInstance.notif()?.ok).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('publiée');
  });

  it('publier (409) : affiche le motif exact du serveur', () => {
    const fixture = load();
    fixture.componentInstance.publish();
    http.expectOne('/api/v1/videos/v1/publish').flush(
      { detail: 'Publication impossible depuis l’état Draft.' },
      { status: 409, statusText: 'Conflict' });
    fixture.detectChanges();
    expect(fixture.componentInstance.notif()?.ok).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Draft');
  });
});
