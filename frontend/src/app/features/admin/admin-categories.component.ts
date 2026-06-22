import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CategoryService } from '../../core/api/category.service';
import { Category } from '../../core/models/video.models';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

@Component({
  selector: 'app-admin-categories',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslatePipe],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">{{ 'admin.categories.title' | t }}</h1>
    <div class="flex gap-2 mb-4 max-w-md">
      <input [(ngModel)]="newName" placeholder="{{ 'admin.categories.placeholder' | t }}" class="flex-1 px-3 py-2 rounded-lg border border-line" />
      <button (click)="create()" [disabled]="!newName" class="px-3 py-2 rounded-lg bg-ink text-white font-semibold disabled:opacity-50">{{ 'action.add' | t }}</button>
    </div>
    <p *ngIf="error()" class="text-sm text-map-red mb-2">{{ error() }}</p>
    <ul class="bg-white border border-line rounded-lg divide-y divide-line max-w-md">
      <li *ngFor="let c of categories()" class="flex items-center gap-2 p-3">
        <span class="flex-1 text-ink">{{ c.name }}</span>
        <button (click)="remove(c)" class="text-sm text-map-red underline">{{ 'action.delete' | t }}</button>
      </li>
    </ul>
  `
})
export class AdminCategoriesComponent implements OnInit {
  private api = inject(CategoryService);
  private ts = inject(TranslationService);
  categories = signal<Category[]>([]);
  newName = '';
  error = signal<string | null>(null);

  ngOnInit(): void { this.load(); }
  private load(): void { this.api.list().subscribe((cs) => this.categories.set(cs)); }

  create(): void {
    if (!this.newName) return;
    this.error.set(null);
    this.api.create(this.newName).subscribe({
      next: () => { this.newName = ''; this.load(); },
      error: (e) => this.error.set(e.error?.detail ?? this.ts.t('admin.categories.createError'))
    });
  }
  remove(c: Category): void {
    this.error.set(null);
    this.api.delete(c.id).subscribe({
      next: () => this.load(),
      error: (e) => this.error.set(e.error?.detail ?? this.ts.t('admin.categories.deleteError'))
    });
  }
}
