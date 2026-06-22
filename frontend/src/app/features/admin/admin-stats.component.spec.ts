import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdminStatsComponent } from './admin-stats.component';

describe('AdminStatsComponent', () => {
  let http: HttpTestingController;
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AdminStatsComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    http = TestBed.inject(HttpTestingController);
  });

  it('charge et affiche les statistiques', () => {
    const fixture = TestBed.createComponent(AdminStatsComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/stats').flush({
      totalVideos: 248, videosByStatus: { Published: 100 }, totalUsers: 12,
      totalViews: 12480, totalWatchSeconds: 99999, topVideos: [{ id: 'v1', title: 'Top sujet', views: 500 }]
    });
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('248');
    expect(text).toContain('Top sujet');
  });

  it('en cas d’erreur API : affiche une erreur (pas de spinner infini)', () => {
    const fixture = TestBed.createComponent(AdminStatsComponent);
    fixture.detectChanges();
    http.expectOne('/api/v1/stats').flush('boom', { status: 500, statusText: 'Server Error' });
    fixture.detectChanges();
    expect(fixture.componentInstance.error()).toBe(true);
  });
});
