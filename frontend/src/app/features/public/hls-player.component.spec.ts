import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { HlsPlayerComponent } from './hls-player.component';

describe('HlsPlayerComponent', () => {
  it('expose un élément vidéo et accepte un manifeste', async () => {
    await TestBed.configureTestingModule({
      imports: [HlsPlayerComponent],
      providers: [provideHttpClient(), provideHttpClientTesting()]
    }).compileComponents();
    const fixture = TestBed.createComponent(HlsPlayerComponent);
    fixture.componentRef.setInput('manifestUrl', '/api/v1/videos/v1/hls/master.m3u8');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('video')).toBeTruthy();
    fixture.destroy();
  });
});
