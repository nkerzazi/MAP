import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { AuthService } from './auth.service';
import { AuthResponse, AuthUser, LoginRequest, RegisterRequest } from './auth.models';

const TOKEN_KEY = 'map_token';
const USER_KEY = 'map_user';

@Injectable({ providedIn: 'root' })
export class AuthStore {
  private auth = inject(AuthService);
  private _token = signal<string | null>(localStorage.getItem(TOKEN_KEY));
  private _user = signal<AuthUser | null>(AuthStore.restoreUser());

  token = this._token.asReadonly();
  user = this._user.asReadonly();
  isAuthenticated = computed(() => !!this._token());
  roles = computed(() => this._user()?.roles ?? []);

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  login(body: LoginRequest): Observable<AuthResponse> {
    return this.auth.login(body).pipe(tap((res) => this.apply(res)));
  }
  register(body: RegisterRequest): Observable<AuthResponse> {
    return this.auth.register(body).pipe(tap((res) => this.apply(res)));
  }
  loadSession(): Observable<AuthUser> {
    return this.auth.me().pipe(tap((u) => this._user.set(u)));
  }
  logout(): void {
    this._token.set(null);
    this._user.set(null);
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  }

  private apply(res: AuthResponse): void {
    const user: AuthUser = { userId: res.userId, email: res.email, displayName: res.displayName, roles: res.roles };
    this._token.set(res.token);
    this._user.set(user);
    localStorage.setItem(TOKEN_KEY, res.token);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
  }

  /** Restaure l'utilisateur (et donc ses rôles) depuis localStorage au démarrage,
   *  pour que les gardes de rôle survivent à un rafraîchissement de page. */
  private static restoreUser(): AuthUser | null {
    try {
      const raw = localStorage.getItem(USER_KEY);
      return raw ? (JSON.parse(raw) as AuthUser) : null;
    } catch {
      return null;
    }
  }
}
