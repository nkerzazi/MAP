import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CatalogService } from '../../core/api/catalog.service';
import { CategoryService } from '../../core/api/category.service';
import { EditorVideoService } from '../../core/api/editor-video.service';
import { Category } from '../../core/models/video.models';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

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
      <p *ngIf="message()" class="text-sm text-emerald-700">{{ message() }}</p>
    </div>
  `
})
export class VideoEditComponent implements OnInit {
  @Input({ required: true }) id!: string;
  private catalog = inject(CatalogService);
  private categoryApi = inject(CategoryService);
  private api = inject(EditorVideoService);

  title = '';
  description = '';
  categoryId: string | null = null;
  tagsCsv = '';
  status = signal('Draft');
  categories = signal<Category[]>([]);
  message = signal<string | null>(null);

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
    }).subscribe((v) => { this.status.set(v.status); this.message.set('Métadonnées enregistrées.'); });
  }
  publish(): void {
    this.api.publish(this.id).subscribe(() => { this.status.set('Published'); this.message.set('Vidéo publiée.'); });
  }
  archive(): void {
    this.api.archive(this.id).subscribe(() => { this.status.set('Archived'); this.message.set('Vidéo archivée.'); });
  }
}
