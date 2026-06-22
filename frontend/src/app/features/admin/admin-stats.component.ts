import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminService } from '../../core/admin/admin.service';
import { StatsSummary } from '../../core/admin/admin.models';
import { SpinnerComponent } from '../../shared/ui/spinner.component';

@Component({
  selector: 'app-admin-stats',
  standalone: true,
  imports: [CommonModule, SpinnerComponent],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">Tableau de bord</h1>
    <div *ngIf="!stats()" class="flex justify-center py-10"><app-spinner></app-spinner></div>
    <div *ngIf="stats() as s" class="space-y-6">
      <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
        <div class="bg-white border border-line rounded-lg p-4 shadow-soft"><div class="text-2xl font-bold text-ink">{{ s.totalVideos }}</div><div class="text-xs text-muted">vidéos</div></div>
        <div class="bg-white border border-line rounded-lg p-4 shadow-soft"><div class="text-2xl font-bold text-ink">{{ s.totalUsers }}</div><div class="text-xs text-muted">utilisateurs</div></div>
        <div class="bg-white border border-line rounded-lg p-4 shadow-soft"><div class="text-2xl font-bold text-ink">{{ s.totalViews }}</div><div class="text-xs text-muted">vues</div></div>
        <div class="bg-white border border-line rounded-lg p-4 shadow-soft"><div class="text-2xl font-bold text-ink">{{ hours(s.totalWatchSeconds) }}</div><div class="text-xs text-muted">heures vues</div></div>
      </div>
      <div class="bg-white border border-line rounded-lg p-4">
        <h2 class="font-semibold text-ink mb-2">Top contenus</h2>
        <ol class="list-decimal ms-5 space-y-1 text-sm text-ink">
          <li *ngFor="let v of s.topVideos">{{ v.title }} — <span class="text-muted">{{ v.views }} vues</span></li>
        </ol>
      </div>
    </div>
  `
})
export class AdminStatsComponent implements OnInit {
  private api = inject(AdminService);
  stats = signal<StatsSummary | null>(null);
  ngOnInit(): void { this.api.stats().subscribe((s) => this.stats.set(s)); }
  hours(seconds: number): number { return Math.round(seconds / 3600); }
}
