export interface AuthResponse {
  token: string;
  expiresAt: string;
  userId: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface AuthUser {
  userId: string;
  email: string;
  displayName: string;
  roles: string[];
}

export interface LoginRequest { email: string; password: string; }
export interface RegisterRequest { email: string; password: string; displayName: string; }
