import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { PagedResult } from '../models/video.models';
import { AuditEntry, StatsSummary } from './admin.models';

@Injectable({ providedIn: 'root' })
export class AdminService {
  private http = inject(HttpClient);
  private base = environment.apiBase;

  stats(): Observable<StatsSummary> {
    return this.http.get<StatsSummary>(`${this.base}/stats`);
  }
  audit(page = 1): Observable<PagedResult<AuditEntry>> {
    return this.http.get<PagedResult<AuditEntry>>(`${this.base}/audit`, { params: new HttpParams().set('page', String(page)) });
  }
  config(): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>(`${this.base}/config`);
  }
  setConfig(key: string, value: string): Observable<void> {
    return this.http.put<void>(`${this.base}/config`, { key, value });
  }
}
