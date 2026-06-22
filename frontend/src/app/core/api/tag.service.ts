import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { Tag } from '../models/video.models';

@Injectable({ providedIn: 'root' })
export class TagService {
  private http = inject(HttpClient);
  list(q?: string): Observable<Tag[]> {
    let params = new HttpParams();
    if (q) params = params.set('q', q);
    return this.http.get<Tag[]>(`${environment.apiBase}/tags`, { params });
  }
}
