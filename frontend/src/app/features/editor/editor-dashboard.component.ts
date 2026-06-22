import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EditorVideoService } from '../../core/api/editor-video.service';
import { VideoListItem } from '../../core/models/video.models';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';
import { SpinnerComponent } from '../../shared/ui/spinner.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-editor-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, StatusBadgeComponent, SpinnerComponent, TranslatePipe],
  template: `
    <div class="flex items-center mb-4">
      <h1 class="text-xl font-bold text-ink">{{ 'editor.dashboard.title' | t }}</h1>
      <a routerLink="/studio/upload" class="ms-auto px-3 py-2 rounded-lg bg-map-red text-white text-sm font-semibold">{{ 'editor.dashboard.upload' | t }}</a>
    </div>

    <div *ngIf="loading()" class="flex justify-center py-10"><app-spinner></app-spinner></div>

    <table *ngIf="!loading()" class="w-full bg-white border border-line rounded-lg overflow-hidden">
      <thead class="text-left text-xs text-muted border-b border-line">
        <tr><th class="p-3">{{ 'editor.col.title' | t }}</th><th class="p-3">{{ 'editor.col.status' | t }}</th><th class="p-3"></th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let v of items()" class="border-b border-line last:border-0">
          <td class="p-3 text-ink font-medium">{{ v.title }}</td>
          <td class="p-3"><app-status-badge [status]="v.status"></app-status-badge></td>
          <td class="p-3 text-end"><a [routerLink]="['/studio/videos', v.id, 'edit']" class="text-ink underline text-sm">{{ 'action.edit' | t }}</a></td>
        </tr>
        <tr *ngIf="items().length === 0"><td colspan="3" class="p-6 text-center text-muted">{{ 'editor.dashboard.empty' | t }}</td></tr>
      </tbody>
    </table>
  `
})
export class EditorDashboardComponent implements OnInit {
  private api = inject(EditorVideoService);
  items = signal<VideoListItem[]>([]);
  loading = signal(true);

  ngOnInit(): void {
    this.api.mine(1).subscribe((res) => { this.items.set(res.items); this.loading.set(false); });
  }
}
