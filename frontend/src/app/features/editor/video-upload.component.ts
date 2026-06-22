import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { EditorVideoService } from '../../core/api/editor-video.service';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

const CHUNK_SIZE = 5 * 1024 * 1024;

@Component({
  selector: 'app-video-upload',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">{{ 'editor.upload.title' | t }}</h1>
    <div class="max-w-lg space-y-3 bg-white border border-line rounded-lg p-5">
      <input [(ngModel)]="title" name="title" required placeholder="{{ 'editor.upload.titlePlaceholder' | t }}"
             class="w-full px-3 py-2 rounded-lg border border-line" />
      <textarea [(ngModel)]="description" name="description" placeholder="{{ 'editor.upload.descPlaceholder' | t }}"
                class="w-full px-3 py-2 rounded-lg border border-line"></textarea>
      <input type="file" accept="video/*" (change)="onFile($event)" />
      <div *ngIf="uploading()" class="h-2 bg-line rounded-full overflow-hidden">
        <div class="h-full bg-ink" [style.width.%]="progress()"></div>
      </div>
      <p *ngIf="error()" class="text-sm text-map-red">{{ error() }}</p>
      <button (click)="submit()" [disabled]="uploading() || !file || !title"
              class="px-4 py-2 rounded-lg bg-ink text-white font-semibold disabled:opacity-50">{{ 'editor.upload.start' | t }}</button>
    </div>
  `
})
export class VideoUploadComponent {
  private api = inject(EditorVideoService);
  private router = inject(Router);
  private ts = inject(TranslationService);
  title = '';
  description = '';
  file: File | null = null;
  uploading = signal(false);
  progress = signal(0);
  error = signal<string | null>(null);

  onFile(e: Event): void {
    this.file = (e.target as HTMLInputElement).files?.[0] ?? null;
  }

  async submit(): Promise<void> {
    if (!this.file || !this.title) return;
    this.uploading.set(true);
    this.error.set(null);
    try {
      const { id } = await firstValueFrom(
        this.api.create({ title: this.title, description: this.description || undefined, categoryId: undefined })
      );
      const total = Math.ceil(this.file.size / CHUNK_SIZE);
      for (let i = 0; i < total; i++) {
        const slice = this.file.slice(i * CHUNK_SIZE, (i + 1) * CHUNK_SIZE);
        await firstValueFrom(this.api.uploadChunk(id, i, slice));
        this.progress.set(Math.round(((i + 1) / total) * 100));
      }
      await firstValueFrom(this.api.complete(id, total));
      this.router.navigate(['/studio/videos', id, 'edit']);
    } catch {
      this.error.set(this.ts.t('editor.upload.error'));
    } finally {
      this.uploading.set(false);
    }
  }
}
