import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminService } from '../../core/admin/admin.service';
import { AuditEntry } from '../../core/admin/admin.models';
import { PaginationComponent } from '../../shared/ui/pagination.component';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

@Component({
  selector: 'app-admin-audit',
  standalone: true,
  imports: [CommonModule, PaginationComponent, TranslatePipe],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">{{ 'admin.audit.title' | t }}</h1>
    <table class="w-full bg-white border border-line rounded-lg overflow-hidden text-sm">
      <thead class="text-left text-xs text-muted border-b border-line">
        <tr><th class="p-3">{{ 'admin.audit.col.date' | t }}</th><th class="p-3">{{ 'admin.audit.col.action' | t }}</th><th class="p-3">{{ 'admin.audit.col.entity' | t }}</th><th class="p-3">{{ 'admin.audit.col.actor' | t }}</th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let e of entries()" class="border-b border-line last:border-0">
          <td class="p-3 text-muted">{{ e.occurredAt | date: 'short' }}</td>
          <td class="p-3 text-ink font-medium">{{ e.action }}</td>
          <td class="p-3">{{ e.entityType }} <span class="text-muted">{{ e.entityId }}</span></td>
          <td class="p-3 text-muted">{{ e.actorId }}</td>
        </tr>
      </tbody>
    </table>
    <div class="mt-4">
      <app-pagination [total]="total()" [pageSize]="pageSize()" [page]="page()" (pageChange)="goToPage($event)"></app-pagination>
    </div>
  `
})
export class AdminAuditComponent implements OnInit {
  private api = inject(AdminService);
  entries = signal<AuditEntry[]>([]);
  total = signal(0);
  page = signal(1);
  pageSize = signal(20);

  ngOnInit(): void { this.load(); }
  private load(): void {
    this.api.audit(this.page()).subscribe((res) => {
      this.entries.set(res.items);
      this.total.set(res.total);
      this.pageSize.set(res.pageSize);
    });
  }
  goToPage(p: number): void { this.page.set(p); this.load(); }
}
