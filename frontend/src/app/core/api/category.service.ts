import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from 'src/environments/environment';
import { Category } from '../models/video.models';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private http = inject(HttpClient);
  private base = `${environment.apiBase}/categories`;

  list(): Observable<Category[]> {
    return this.http.get<Category[]>(this.base);
  }
  create(name: string): Observable<Category> {
    return this.http.post<Category>(this.base, { name });
  }
  update(id: string, name: string): Observable<Category> {
    return this.http.put<Category>(`${this.base}/${id}`, { name });
  }
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }
}
