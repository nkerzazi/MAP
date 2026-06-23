import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
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

      <input id="video-file" type="file" accept="video/*" (change)="onFile($event)" class="hidden" />
      <div class="flex items-center gap-3">
        <label for="video-file"
               class="cursor-pointer inline-block px-4 py-2 rounded-lg border border-ink text-ink font-semibold hover:bg-paper">
          {{ 'editor.upload.choose' | t }}
        </label>
        <span class="text-sm" [class.text-ink]="file" [class.text-muted]="!file">
          {{ file ? file.name : ('editor.upload.noFile' | t) }}
        </span>
      </div>

      <div *ngIf="uploading()" class="h-2 bg-line rounded-full overflow-hidden">
        <div class="h-full bg-ink" [style.width.%]="progress()"></div>
      </div>
      <div *ngIf="error()" class="space-y-1">
        <p class="text-sm text-map-red font-semibold">{{ error() }}</p>
        <details *ngIf="errorDetail()" class="text-xs">
          <summary class="cursor-pointer text-muted">{{ 'editor.upload.errDetail' | t }}</summary>
          <pre class="mt-1 p-2 bg-paper border border-line rounded overflow-auto max-h-48 whitespace-pre-wrap text-ink">{{ errorDetail() }}</pre>
        </details>
      </div>
      <button (click)="submit()" [disabled]="uploading() || !file || !title"
              class="px-4 py-2 rounded-lg bg-ink text-white font-semibold disabled:opacity-50">{{ 'editor.upload.start' | t }}</button>
      <p *ngIf="!uploading() && (!file || !title)" class="text-xs text-muted">{{ 'editor.upload.hint' | t }}</p>
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
  errorDetail = signal<string | null>(null);

  onFile(e: Event): void {
    this.file = (e.target as HTMLInputElement).files?.[0] ?? null;
  }

  async submit(): Promise<void> {
    if (!this.file || !this.title) return;
    this.uploading.set(true);
    this.error.set(null);
    this.errorDetail.set(null);
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
    } catch (e) {
      this.error.set(this.friendlyMessage(e));
      this.errorDetail.set(this.technicalDetail(e));
    } finally {
      this.uploading.set(false);
    }
  }

  /** Message clair adapté au type d'erreur. */
  private friendlyMessage(e: unknown): string {
    if (e instanceof HttpErrorResponse) {
      if (e.status === 0) return this.ts.t('editor.upload.errServer');
      if (e.status === 401 || e.status === 403) return this.ts.t('editor.upload.errAuth');
      if (e.status === 413) return this.ts.t('editor.upload.errTooLarge');
    }
    return this.ts.t('editor.upload.error');
  }

  /** Détail technique (statut, URL, ProblemDetails serveur, ou stack JS) pour le diagnostic. */
  private technicalDetail(e: unknown): string {
    if (e instanceof HttpErrorResponse) {
      const server = e.error?.detail ?? e.error?.title ?? (typeof e.error === 'string' ? e.error : '');
      return [`HTTP ${e.status} ${e.statusText}`, e.url ?? '', server, e.message]
        .filter((s) => s).join('\n');
    }
    if (e instanceof Error) return `${e.name}: ${e.message}\n${e.stack ?? ''}`.trim();
    return String(e);
  }
}
