import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./layouts/public-shell.component').then((m) => m.PublicShellComponent),
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/public/catalog-page.component').then((m) => m.CatalogPageComponent)
      },
      {
        path: 'videos/:id',
        loadComponent: () =>
          import('./features/public/video-detail-page.component').then((m) => m.VideoDetailPageComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
