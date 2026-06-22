import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CatalogService } from '../../core/api/catalog.service';
import { CategoryService } from '../../core/api/category.service';
import { Category, VideoListItem } from '../../core/models/video.models';
import { VideoCardComponent } from '../../shared/ui/video-card.component';
import { SearchBarComponent } from '../../shared/ui/search-bar.component';
import { PaginationComponent } from '../../shared/ui/pagination.component';
import { SpinnerComponent } from '../../shared/ui/spinner.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-catalog-page',
  standalone: true,
  imports: [CommonModule, VideoCardComponent, SearchBarComponent, PaginationComponent, SpinnerComponent, TranslatePipe],
  template: `
    <section class="space-y-5">
      <div class="flex flex-col gap-3 sm:flex-row sm:items-center">
        <h1 class="text-xl font-bold text-ink">{{ 'catalog.title' | t }}</h1>
        <div class="sm:ms-auto sm:w-80"><app-search-bar (search)="onSearch($event)"></app-search-bar></div>
      </div>

      <div class="flex flex-wrap gap-2">
        <button (click)="filterCategory(null)"
                class="px-3 py-1 rounded-full text-sm border"
                [class]="!categoryId() ? 'bg-ink text-white border-ink' : 'border-line text-ink'">{{ 'catalog.all' | t }}</button>
        <button *ngFor="let c of categories()" (click)="filterCategory(c.id)"
                class="px-3 py-1 rounded-full text-sm border"
                [class]="categoryId() === c.id ? 'bg-ink text-white border-ink' : 'border-line text-ink'">{{ c.name }}</button>
      </div>

      <div *ngIf="loading()" class="flex justify-center py-10"><app-spinner></app-spinner></div>

      <p *ngIf="error()" class="text-map-red py-10 text-center">{{ 'state.error' | t }}</p>

      <div *ngIf="!loading() && !error()">
        <p *ngIf="items().length === 0" class="text-muted py-10 text-center">{{ 'catalog.empty' | t }}</p>
        <div class="grid grid-cols-2 md:grid-cols-3 gap-4">
          <app-video-card *ngFor="let v of items()" [video]="v"></app-video-card>
        </div>
        <div class="mt-6">
          <app-pagination [total]="total()" [pageSize]="pageSize()" [page]="page()" (pageChange)="goToPage($event)"></app-pagination>
        </div>
      </div>
    </section>
  `
})
export class CatalogPageComponent implements OnInit {
  private catalog = inject(CatalogService);
  private categoryApi = inject(CategoryService);

  items = signal<VideoListItem[]>([]);
  categories = signal<Category[]>([]);
  total = signal(0);
  page = signal(1);
  pageSize = signal(20);
  loading = signal(false);
  error = signal(false);
  q = signal('');
  categoryId = signal<string | null>(null);

  ngOnInit(): void {
    this.categoryApi.list().subscribe((cs) => this.categories.set(cs));
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.catalog
      .list({ q: this.q() || undefined, categoryId: this.categoryId() ?? undefined, page: this.page() })
      .subscribe({
        next: (res) => {
          this.items.set(res.items);
          this.total.set(res.total);
          this.pageSize.set(res.pageSize);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.error.set(true);
        }
      });
  }

  onSearch(term: string): void { this.q.set(term); this.page.set(1); this.load(); }
  filterCategory(id: string | null): void { this.categoryId.set(id); this.page.set(1); this.load(); }
  goToPage(p: number): void { this.page.set(p); this.load(); }
}
