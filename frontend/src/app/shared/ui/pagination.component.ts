import { Component, EventEmitter, Input, Output, computed, signal } from '@angular/core';
import { NgIf } from '@angular/common';

@Component({
  selector: 'app-pagination',
  standalone: true,
  imports: [NgIf],
  template: `
    <nav class="flex items-center justify-center gap-2" *ngIf="totalPages() > 1">
      <button (click)="go(current() - 1)" [disabled]="current() === 1"
              class="px-3 py-1.5 rounded-lg border border-line disabled:opacity-40">Précédent</button>
      <span class="text-sm text-muted">Page {{ current() }} / {{ totalPages() }}</span>
      <button (click)="go(current() + 1)" [disabled]="current() === totalPages()"
              class="px-3 py-1.5 rounded-lg border border-line disabled:opacity-40">Suivant</button>
    </nav>
  `
})
export class PaginationComponent {
  private _total = signal(0);
  private _size = signal(20);
  current = signal(1);

  @Input({ required: true }) set total(v: number) { this._total.set(v); }
  @Input() set pageSize(v: number) { this._size.set(v || 20); }
  @Input({ required: true }) set page(v: number) { this.current.set(v); }
  @Output() pageChange = new EventEmitter<number>();

  totalPages = computed(() => Math.max(1, Math.ceil(this._total() / this._size())));

  go(p: number): void {
    if (p < 1 || p > this.totalPages()) return;
    this.pageChange.emit(p);
  }
}
