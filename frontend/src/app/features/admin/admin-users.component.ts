import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AdminUserService } from '../../core/admin/admin-user.service';
import { UserAdminItem } from '../../core/admin/admin.models';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

const ROLES = ['Visiteur', 'Editeur', 'Admin'];

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [CommonModule, TranslatePipe],
  template: `
    <h1 class="text-xl font-bold text-ink mb-4">{{ 'admin.users.title' | t }}</h1>
    <table class="w-full bg-white border border-line rounded-lg overflow-hidden text-sm">
      <thead class="text-left text-xs text-muted border-b border-line">
        <tr><th class="p-3">{{ 'admin.users.col.email' | t }}</th><th class="p-3">{{ 'admin.users.col.name' | t }}</th><th class="p-3">{{ 'admin.users.col.roles' | t }}</th><th class="p-3">{{ 'admin.users.col.active' | t }}</th><th class="p-3">{{ 'admin.users.col.assign' | t }}</th></tr>
      </thead>
      <tbody>
        <tr *ngFor="let u of users()" class="border-b border-line last:border-0">
          <td class="p-3 text-ink">{{ u.email }}</td>
          <td class="p-3">{{ u.displayName }}</td>
          <td class="p-3">
            <span *ngFor="let r of u.roles" class="inline-flex items-center gap-1 me-1 px-2 py-0.5 rounded-full bg-line text-xs">
              {{ r }} <button (click)="remove(u.id, r)" class="text-map-red">×</button>
            </span>
          </td>
          <td class="p-3">
            <button (click)="toggleActive(u)" class="underline" [class.text-emerald-700]="u.isActive" [class.text-muted]="!u.isActive">
              {{ (u.isActive ? 'admin.users.yes' : 'admin.users.no') | t }}
            </button>
          </td>
          <td class="p-3">
            <select #sel class="border border-line rounded px-2 py-1" (change)="assign(u.id, sel.value); sel.value=''">
              <option value="">{{ 'admin.users.addRole' | t }}</option>
              <option *ngFor="let r of roles" [value]="r">{{ r }}</option>
            </select>
          </td>
        </tr>
      </tbody>
    </table>
  `
})
export class AdminUsersComponent implements OnInit {
  private api = inject(AdminUserService);
  users = signal<UserAdminItem[]>([]);
  roles = ROLES;

  ngOnInit(): void { this.load(); }
  private load(): void { this.api.list().subscribe((u) => this.users.set(u)); }

  assign(id: string, role: string): void {
    if (!role) return;
    this.api.assignRole(id, role).subscribe(() => this.load());
  }
  remove(id: string, role: string): void {
    this.api.removeRole(id, role).subscribe(() => this.load());
  }
  toggleActive(u: UserAdminItem): void {
    this.api.update(u.id, { isActive: !u.isActive, displayName: u.displayName }).subscribe(() => this.load());
  }
}
