import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { PagedResult, VideoDetail, VideoListItem } from '../models/video.models';

export interface CreateVideoBody { title: string; description?: string; categoryId?: string; }
export interface UpdateVideoBody { title: string; description: string | null; categoryId: string | null; tags: string[]; }

@Injectable({ providedIn: 'root' })
export class EditorVideoService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/videos`;

  mine(page = 1): Observable<PagedResult<VideoListItem>> {
    return this.http.get<PagedResult<VideoListItem>>(`${this.base}/mine`, { params: new HttpParams().set('page', String(page)) });
  }
  create(body: CreateVideoBody): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.base, body);
  }
  uploadChunk(id: string, index: number, chunk: Blob): Observable<{ received: number }> {
    return this.http.post<{ received: number }>(`${this.base}/${id}/upload/chunk`, chunk, {
      params: new HttpParams().set('index', String(index))
    });
  }
  complete(id: string, total: number): Observable<{ id: string; status: string }> {
    return this.http.post<{ id: string; status: string }>(`${this.base}/${id}/upload/complete`, null, {
      params: new HttpParams().set('total', String(total))
    });
  }
  update(id: string, body: UpdateVideoBody): Observable<VideoDetail> {
    return this.http.put<VideoDetail>(`${this.base}/${id}`, body);
  }
  publish(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/publish`, null);
  }
  archive(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/${id}/archive`, null);
  }
}
