import { Routes } from '@angular/router';
import { adminGuard, editorGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'auth/login',
    loadComponent: () => import('./features/auth/login-page.component').then((m) => m.LoginPageComponent)
  },
  {
    path: 'auth/register',
    loadComponent: () => import('./features/auth/register-page.component').then((m) => m.RegisterPageComponent)
  },
  {
    path: 'studio',
    canActivate: [editorGuard],
    loadComponent: () => import('./layouts/editor-shell.component').then((m) => m.EditorShellComponent),
    children: [
      { path: '', loadComponent: () => import('./features/editor/editor-dashboard.component').then((m) => m.EditorDashboardComponent) },
      { path: 'upload', loadComponent: () => import('./features/editor/video-upload.component').then((m) => m.VideoUploadComponent) },
      { path: 'videos/:id/edit', loadComponent: () => import('./features/editor/video-edit.component').then((m) => m.VideoEditComponent) }
    ]
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./layouts/admin-shell.component').then((m) => m.AdminShellComponent),
    children: [
      { path: '', loadComponent: () => import('./features/admin/admin-stats.component').then((m) => m.AdminStatsComponent) },
      { path: 'users', loadComponent: () => import('./features/admin/admin-users.component').then((m) => m.AdminUsersComponent) },
      { path: 'categories', loadComponent: () => import('./features/admin/admin-categories.component').then((m) => m.AdminCategoriesComponent) },
      { path: 'audit', loadComponent: () => import('./features/admin/admin-audit.component').then((m) => m.AdminAuditComponent) },
      { path: 'config', loadComponent: () => import('./features/admin/admin-config.component').then((m) => m.AdminConfigComponent) }
    ]
  },
  {
    path: '',
    loadComponent: () => import('./layouts/public-shell.component').then((m) => m.PublicShellComponent),
    children: [
      { path: '', loadComponent: () => import('./features/public/catalog-page.component').then((m) => m.CatalogPageComponent) },
      { path: 'videos/:id', loadComponent: () => import('./features/public/video-detail-page.component').then((m) => m.VideoDetailPageComponent) }
    ]
  },
  { path: '**', redirectTo: '' }
];
