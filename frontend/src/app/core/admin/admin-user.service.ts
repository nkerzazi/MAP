import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { UserAdminItem } from './admin.models';

@Injectable({ providedIn: 'root' })
export class AdminUserService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/users`;

  list(): Observable<UserAdminItem[]> {
    return this.http.get<UserAdminItem[]>(this.base);
  }
  update(id: string, body: { isActive: boolean; displayName?: string }): Observable<void> {
    return this.http.put<void>(`${this.base}/${id}`, body);
  }
  assignRole(id: string, role: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/roles`, { role });
  }
  removeRole(id: string, role: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}/roles/${role}`);
  }
}
