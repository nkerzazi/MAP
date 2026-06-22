import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';

export interface RecordViewBody { watchSeconds: number; sessionId: string; }

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private http = inject(HttpClient);
  recordView(videoId: string, body: RecordViewBody): Observable<void> {
    return this.http.post<void>(`${environment.apiBase}/videos/${videoId}/views`, body);
  }
}
