import { Component, ElementRef, Input, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import Hls from 'hls.js';
import { AnalyticsService } from '../../core/api/analytics.service';

@Component({
  selector: 'app-hls-player',
  standalone: true,
  template: `<video #video controls playsinline class="w-full rounded-lg bg-black"></video>`
})
export class HlsPlayerComponent implements OnInit, OnDestroy {
  @Input({ required: true }) manifestUrl!: string;
  @Input() videoId?: string;
  @ViewChild('video', { static: true }) videoRef!: ElementRef<HTMLVideoElement>;

  private analytics = inject(AnalyticsService);
  private hls?: Hls;
  private timer?: ReturnType<typeof setInterval>;
  private readonly sessionId = Math.random().toString(36).slice(2);

  ngOnInit(): void {
    const video = this.videoRef.nativeElement;
    if (Hls.isSupported()) {
      this.hls = new Hls();
      this.hls.loadSource(this.manifestUrl);
      this.hls.attachMedia(video);
    } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
      video.src = this.manifestUrl;
    }
    // Télémétrie : remonte le temps visionné toutes les 15 s pendant la lecture.
    this.timer = setInterval(() => {
      if (this.videoId && !video.paused && video.currentTime > 0) {
        this.analytics.recordView(this.videoId, { watchSeconds: 15, sessionId: this.sessionId }).subscribe();
      }
    }, 15000);
  }

  ngOnDestroy(): void {
    if (this.timer) clearInterval(this.timer);
    this.hls?.destroy();
  }
}
