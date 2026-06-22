import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DatePipe, NgIf } from '@angular/common';
import { VideoListItem } from '../../core/models/video.models';

@Component({
  selector: 'app-video-card',
  standalone: true,
  imports: [RouterLink, DatePipe, NgIf],
  template: `
    <a [routerLink]="['/videos', video.id]"
       class="block rounded-lg overflow-hidden bg-white border border-line shadow-soft hover:shadow-md transition-shadow">
      <div class="aspect-video bg-gradient-to-br from-ink-600 to-ink-500"></div>
      <div class="p-3">
        <h3 class="font-semibold text-ink line-clamp-2">{{ video.title }}</h3>
        <p class="mt-1 text-xs text-muted">
          {{ video.categoryName }}
          <span *ngIf="video.publishedAt">· {{ video.publishedAt | date: 'mediumDate' }}</span>
        </p>
      </div>
    </a>
  `
})
export class VideoCardComponent {
  @Input({ required: true }) video!: VideoListItem;
}
