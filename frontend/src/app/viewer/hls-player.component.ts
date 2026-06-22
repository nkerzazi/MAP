import { Component, ElementRef, Input, OnDestroy, OnInit, ViewChild } from '@angular/core';
import Hls from 'hls.js';

/**
 * Lecteur vidéo interne basé sur hls.js.
 * Reçoit l'URL du manifeste HLS retournée par GET /api/v1/videos/{id}/stream,
 * ce qui rend le lecteur intégrable ailleurs sur le site de la MAP (accessible via API).
 * Squelette — UI/contrôles et télémétrie de visionnage ajoutés en phase « Diffusion & viewer ».
 */
@Component({
  selector: 'app-hls-player',
  standalone: true,
  template: `<video #video controls playsinline style="width:100%"></video>`,
})
export class HlsPlayerComponent implements OnInit, OnDestroy {
  @Input() manifestUrl!: string;
  @ViewChild('video', { static: true }) videoRef!: ElementRef<HTMLVideoElement>;
  private hls?: Hls;

  ngOnInit(): void {
    const video = this.videoRef.nativeElement;
    if (Hls.isSupported()) {
      this.hls = new Hls();
      this.hls.loadSource(this.manifestUrl);
      this.hls.attachMedia(video);
    } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
      video.src = this.manifestUrl; // Safari : HLS natif
    }
  }

  ngOnDestroy(): void {
    this.hls?.destroy();
  }
}
