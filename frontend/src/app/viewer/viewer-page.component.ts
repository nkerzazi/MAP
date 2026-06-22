import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { HlsPlayerComponent } from './hls-player.component';

/**
 * Page de visionnage : récupère l'URL du manifeste via GET /api/v1/videos/{id}/stream
 * puis délègue la lecture au composant hls.js. Branche le lecteur sur l'API de diffusion (Phase 4).
 */
@Component({
  selector: 'app-viewer-page',
  standalone: true,
  imports: [CommonModule, HlsPlayerComponent],
  template: `
    <app-hls-player *ngIf="manifestUrl" [manifestUrl]="manifestUrl"></app-hls-player>
  `,
})
export class ViewerPageComponent implements OnInit {
  @Input() videoId!: string;
  manifestUrl?: string;

  constructor(private http: HttpClient) {}

  ngOnInit(): void {
    this.http
      .get<{ manifestUrl: string }>(`/api/v1/videos/${this.videoId}/stream`)
      .subscribe((res) => (this.manifestUrl = res.manifestUrl));
  }
}
