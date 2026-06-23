import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { CatalogService } from '../../core/api/catalog.service';
import { CategoryService } from '../../core/api/category.service';
import { EditorVideoService } from '../../core/api/editor-video.service';
import { Category } from '../../core/models/video.models';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-video-edit',
  standalone: true,
  imports: [CommonModule, FormsModule, StatusBadgeComponent, TranslatePipe],
  template: `
    <div class="max-w-lg space-y-3">
      <div class="flex items-center gap-3">
        <h1 class="text-xl font-bold text-ink">{{ 'editor.edit.title' | t }}</h1>
        <app-status-badge [status]="status()"></app-status-badge>
      </div>
      <input [(ngModel)]="title" name="title" placeholder="{{ 'editor.upload.titlePlaceholder' | t }}" class="w-full px-3 py-2 rounded-lg border border-line" />
      <textarea [(ngModel)]="description" name="description" placeholder="{{ 'editor.upload.descPlaceholder' | t }}" class="w-full px-3 py-2 rounded-lg border border-line"></textarea>
      <select [(ngModel)]="categoryId" name="cat" class="w-full px-3 py-2 rounded-lg border border-line">
        <option [ngValue]="null">{{ 'editor.edit.noCategory' | t }}</option>
        <option *ngFor="let c of categories()" [ngValue]="c.id">{{ c.name }}</option>
      </select>
      <input [(ngModel)]="tagsCsv" name="tags" placeholder="{{ 'editor.edit.tags' | t }}" class="w-full px-3 py-2 rounded-lg border border-line" />
      <div class="flex gap-2">
        <button (click)="save()" class="px-4 py-2 rounded-lg bg-ink text-white font-semibold">{{ 'action.save' | t }}</button>
        <button (click)="publish()" class="px-4 py-2 rounded-lg bg-emerald-600 text-white font-semibold">{{ 'editor.edit.publish' | t }}</button>
        <button (click)="archive()" class="px-4 py-2 rounded-lg border border-line">{{ 'editor.edit.archive' | t }}</button>
      </div>
      <p *ngIf="notif() as n" class="text-sm font-semibold"
         [class.text-emerald-700]="n.ok" [class.text-map-red]="!n.ok">
        {{ (n.ok ? '✓ ' : '⚠ ') + n.text }}
      </p>
    </div>
  `
})
export class VideoEditComponent implements OnInit {
  @Input({ required: true }) id!: string;
  private catalog = inject(CatalogService);
  private categoryApi = inject(CategoryService);
  private api = inject(EditorVideoService);
  private ts = inject(TranslationService);

  title = '';
  description = '';
  categoryId: string | null = null;
  tagsCsv = '';
  status = signal('Draft');
  categories = signal<Category[]>([]);
  notif = signal<{ ok: boolean; text: string } | null>(null);

  ngOnInit(): void {
    this.categoryApi.list().subscribe((cs) => this.categories.set(cs));
    this.catalog.get(this.id).subscribe((v) => {
      this.title = v.title;
      this.description = v.description ?? '';
      this.tagsCsv = v.tags.join(', ');
      this.status.set(v.status);
    });
  }

  private tags(): string[] {
    return this.tagsCsv.split(',').map((t) => t.trim()).filter((t) => t.length > 0);
  }

  save(): void {
    this.api.update(this.id, {
      title: this.title, description: this.description || null, categoryId: this.categoryId, tags: this.tags()
    }).subscribe({
      next: (v) => { this.status.set(v.status); this.ok('editor.edit.saved'); },
      error: (e) => this.fail(e)
    });
  }
  publish(): void {
    this.api.publish(this.id).subscribe({
      next: () => { this.status.set('Published'); this.ok('editor.edit.published'); },
      error: (e) => this.fail(e)
    });
  }
  archive(): void {
    this.api.archive(this.id).subscribe({
      next: () => { this.status.set('Archived'); this.ok('editor.edit.archived'); },
      error: (e) => this.fail(e)
    });
  }

  private ok(key: string): void {
    this.notif.set({ ok: true, text: this.ts.t(key) });
  }
  /** Échec : affiche le motif exact renvoyé par le serveur (ProblemDetails) si présent. */
  private fail(e: unknown): void {
    const detail = e instanceof HttpErrorResponse
      ? (e.error?.detail ?? e.error?.title ?? this.ts.t('editor.edit.actionError'))
      : this.ts.t('editor.edit.actionError');
    this.notif.set({ ok: false, text: detail });
  }
}
