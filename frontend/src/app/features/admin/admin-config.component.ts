import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminService } from '../../core/admin/admin.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

interface ConfigRow { key: string; value: string; }

@Component({
  selector: 'app-admin-config',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">{{ 'admin.config.title' | t }}</h1>
    <div class="space-y-2 max-w-lg">
      <div *ngFor="let row of rows()" class="flex items-center gap-2 bg-white border border-line rounded-lg p-3">
        <span class="w-40 text-sm text-muted">{{ row.key }}</span>
        <input [(ngModel)]="row.value" class="flex-1 px-3 py-1.5 rounded border border-line" />
        <button (click)="save(row.key, row.value)" class="px-3 py-1.5 rounded-lg bg-ink text-white text-sm">{{ 'action.save' | t }}</button>
      </div>
      <div class="flex items-center gap-2 mt-4 bg-white border border-line rounded-lg p-3">
        <input [(ngModel)]="newKey" placeholder="{{ 'admin.config.key' | t }}" class="w-40 px-3 py-1.5 rounded border border-line" />
        <input [(ngModel)]="newValue" placeholder="{{ 'admin.config.value' | t }}" class="flex-1 px-3 py-1.5 rounded border border-line" />
        <button (click)="add()" [disabled]="!newKey" class="px-3 py-1.5 rounded-lg bg-map-red text-white text-sm disabled:opacity-50">{{ 'action.add' | t }}</button>
      </div>
      <p *ngIf="message()" class="text-sm text-emerald-700">{{ message() }}</p>
    </div>
  `
})
export class AdminConfigComponent implements OnInit {
  private api = inject(AdminService);
  rows = signal<ConfigRow[]>([]);
  newKey = '';
  newValue = '';
  message = signal<string | null>(null);

  ngOnInit(): void { this.load(); }
  private load(): void {
    this.api.config().subscribe((cfg) => this.rows.set(Object.entries(cfg).map(([key, value]) => ({ key, value }))));
  }
  save(key: string, value: string): void {
    this.api.setConfig(key, value).subscribe(() => this.message.set(`« ${key} » enregistré.`));
  }
  add(): void {
    if (!this.newKey) return;
    this.api.setConfig(this.newKey, this.newValue).subscribe(() => { this.newKey = ''; this.newValue = ''; this.load(); });
  }
}
