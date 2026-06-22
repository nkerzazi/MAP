import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { VideoDetailPageComponent } from './video-detail-page.component';

describe('VideoDetailPageComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [VideoDetailPageComponent],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge le détail puis le manifeste de streaming', () => {
    const fixture = TestBed.createComponent(VideoDetailPageComponent);
    fixture.componentRef.setInput('id', 'v1');
    fixture.detectChanges();

    http.expectOne('/api/v1/videos/v1').flush({
      id: 'v1', title: 'Sommet économique', description: 'Desc', slug: 'sommet', status: 'Published',
      categoryName: 'Actualités', durationSeconds: 120, publishedAt: null, tags: ['économie'], renditions: []
    });
    http.expectOne('/api/v1/videos/v1/stream').flush({ id: 'v1', manifestUrl: '/api/v1/videos/v1/hls/master.m3u8', renditions: [] });
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Sommet économique');
  });
});
