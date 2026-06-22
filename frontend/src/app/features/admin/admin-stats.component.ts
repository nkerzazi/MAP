import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminService } from '../../core/admin/admin.service';
import { StatsSummary } from '../../core/admin/admin.models';
import { SpinnerComponent } from '../../shared/ui/spinner.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-admin-stats',
  standalone: true,
  imports: [CommonModule, SpinnerComponent, TranslatePipe],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">{{ 'admin.nav.dashboard' | t }}</h1>
    <div *ngIf="!stats() && !error()" class="flex justify-center py-10"><app-spinner></app-spinner></div>
    <p *ngIf="error()" class="text-map-red py-10 text-center">{{ 'state.error' | t }}</p>
    <div *ngIf="stats() as s" class="space-y-6">
      <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div class="bg-white border border-line rounded-lg p-4 shadow-soft"><div class="text-2xl font-bold text-ink">{{ s.totalVideos }}</div><div class="text-xs text-muted">{{ 'admin.stats.videos' | t }}</div></div>
        <div class="bg-white border border-line rounded-lg p-4 shadow-soft"><div class="text-2xl font-bold text-ink">{{ s.totalUsers }}</div><div class="text-xs text-muted">{{ 'admin.stats.users' | t }}</div></div>
        <div class="bg-white border border-line rounded-lg p-4 shadow-soft"><div class="text-2xl font-bold text-ink">{{ s.totalViews }}</div><div class="text-xs text-muted">{{ 'admin.stats.views' | t }}</div></div>
        <div class="bg-white border border-line rounded-lg p-4 shadow-soft"><div class="text-2xl font-bold text-ink">{{ hours(s.totalWatchSeconds) }}</div><div class="text-xs text-muted">{{ 'admin.stats.hours' | t }}</div></div>
      </div>
      <div class="bg-white border border-line rounded-lg p-4">
        <h2 class="font-semibold text-ink mb-2">{{ 'admin.stats.top' | t }}</h2>
        <ol class="list-decimal ms-5 space-y-1 text-sm text-ink">
          <li *ngFor="let v of s.topVideos">{{ v.title }} — <span class="text-muted">{{ v.views }} {{ 'admin.stats.viewsSuffix' | t }}</span></li>
        </ol>
      </div>
    </div>
  `
})
export class AdminStatsComponent implements OnInit {
  private api = inject(AdminService);
  stats = signal<StatsSummary | null>(null);
  error = signal(false);
  ngOnInit(): void {
    this.api.stats().subscribe({ next: (s) => this.stats.set(s), error: () => this.error.set(true) });
  }
  hours(seconds: number): number { return Math.round(seconds / 3600); }
}
