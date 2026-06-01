import { Injectable } from '@angular/core';
import { AuthUser, UserRole } from '../models/auth.models';

// JWT is kept in memory only — never written to localStorage or sessionStorage.
// Refreshing the page requires re-login. Acceptable for MVP.
@Injectable({ providedIn: 'root' })
export class TokenService {
  private _token: string | null = null;
  private _user: AuthUser | null = null;

  setToken(token: string): void {
    this._token = token;
    this._user = this.decode(token);
  }

  getToken(): string | null {
    return this._token;
  }

  getUser(): AuthUser | null {
    return this._user;
  }

  clear(): void {
    this._token = null;
    this._user = null;
  }

  isAuthenticated(): boolean {
    return this._token !== null && !this.isExpired();
  }

  private isExpired(): boolean {
    if (!this._user) return true;
    const payload = this.parsePayload(this._token!);
    if (!payload?.exp) return false;
    return Date.now() / 1000 > payload.exp;
  }

  private decode(token: string): AuthUser | null {
    try {
      const payload = this.parsePayload(token);
      if (!payload) return null;
      return {
        userId: payload.sub,
        email: payload.email,
        role: payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] as UserRole
          ?? payload.role as UserRole,
        mustChangePassword: false, // populated from login response, not JWT
      };
    } catch {
      return null;
    }
  }

  private parsePayload(token: string): any {
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    const decoded = atob(parts[1].replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(decoded);
  }
}
