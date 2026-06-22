export interface VideoListItem {
  id: string;
  title: string;
  slug: string;
  categoryName: string | null;
  durationSeconds: number | null;
  publishedAt: string | null;
  status: string;
}

export interface RenditionInfo {
  resolution: string;
  bitrate: number;
  manifestKey: string;
}

export interface VideoDetail {
  id: string;
  title: string;
  description: string | null;
  slug: string;
  status: string;
  categoryName: string | null;
  durationSeconds: number | null;
  publishedAt: string | null;
  tags: string[];
  renditions: RenditionInfo[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface Category { id: string; name: string; slug: string; }
export interface Tag { id: string; name: string; }

export interface CatalogFilters {
  q?: string;
  categoryId?: string;
  tag?: string;
  page?: number;
}
