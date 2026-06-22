import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from 'src/environments/environment';
import { CatalogService } from '../../core/api/catalog.service';
import { VideoDetail } from '../../core/models/video.models';
import { HlsPlayerComponent } from './hls-player.component';
import { SpinnerComponent } from '../../shared/ui/spinner.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

interface StreamInfo { id: string; manifestUrl: string; renditions: unknown[]; }

@Component({
  selector: 'app-video-detail-page',
  standalone: true,
  imports: [CommonModule, HlsPlayerComponent, SpinnerComponent, TranslatePipe],
  template: `
    <div *ngIf="loading()" class="flex justify-center py-10"><app-spinner></app-spinner></div>
    <p *ngIf="error()" class="text-map-red py-10 text-center">{{ 'state.error' | t }}</p>
    <article *ngIf="video() as v" class="space-y-4">
      <app-hls-player *ngIf="manifestUrl()" [manifestUrl]="manifestUrl()!" [videoId]="v.id"></app-hls-player>
      <h1 class="text-2xl font-bold text-ink">{{ v.title }}</h1>
      <p class="text-ink/80 whitespace-pre-line">{{ v.description }}</p>
      <div class="flex flex-wrap gap-2">
        <span *ngFor="let t of v.tags" class="px-2 py-0.5 rounded-full bg-line text-xs text-ink">#{{ t }}</span>
      </div>
    </article>
  `
})
export class VideoDetailPageComponent implements OnInit {
  @Input({ required: true }) id!: string;
  private catalog = inject(CatalogService);
  private http = inject(HttpClient);

  video = signal<VideoDetail | null>(null);
  manifestUrl = signal<string | null>(null);
  loading = signal(true);
  error = signal(false);

  ngOnInit(): void {
    this.catalog.get(this.id).subscribe({
      next: (v) => {
        this.video.set(v);
        this.loading.set(false);
        this.http.get<StreamInfo>(`${environment.apiBase}/videos/${this.id}/stream`)
          .subscribe({ next: (s) => this.manifestUrl.set(s.manifestUrl), error: () => {} });
      },
      error: () => {
        this.loading.set(false);
        this.error.set(true);
      }
    });
  }
}
