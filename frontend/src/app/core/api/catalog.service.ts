import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { CatalogFilters, PagedResult, VideoDetail, VideoListItem } from '../models/video.models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/videos`;

  list(filters: CatalogFilters = {}): Observable<PagedResult<VideoListItem>> {
    let params = new HttpParams();
    if (filters.q) params = params.set('q', filters.q);
    if (filters.categoryId) params = params.set('categoryId', filters.categoryId);
    if (filters.tag) params = params.set('tag', filters.tag);
    params = params.set('page', String(filters.page ?? 1));
    return this.http.get<PagedResult<VideoListItem>>(this.base, { params });
  }

  get(id: string): Observable<VideoDetail> {
    return this.http.get<VideoDetail>(`${this.base}/${id}`);
  }
}
