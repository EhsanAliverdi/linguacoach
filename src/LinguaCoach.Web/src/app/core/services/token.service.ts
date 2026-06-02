import { Injectable, signal } from '@angular/core';
import { AuthUser, UserRole } from '../models/auth.models';

const SessionKey = 'speakpath.auth';

interface StoredSession {
  token: string;
  mustChangePassword: boolean;
}

@Injectable({ providedIn: 'root' })
export class TokenService {
  private _token: string | null = null;
  readonly currentUser = signal<AuthUser | null>(null);

  constructor() {
    this.restoreSession();
  }

  setToken(token: string, mustChangePassword = false): void {
    this._token = token;
    this.currentUser.set(this.decode(token, mustChangePassword));
    this.persistSession(mustChangePassword);
  }

  getToken(): string | null {
    return this._token;
  }

  getUser(): AuthUser | null {
    return this.currentUser();
  }

  setMustChangePassword(value: boolean): void {
    const user = this.currentUser();
    if (user) this.currentUser.set({ ...user, mustChangePassword: value });
    this.persistSession(value);
  }

  clear(): void {
    this._token = null;
    this.currentUser.set(null);
    sessionStorage.removeItem(SessionKey);
  }

  isAuthenticated(): boolean {
    return this._token !== null && !this.isExpired();
  }

  private isExpired(): boolean {
    if (!this.currentUser()) return true;
    const payload = this.parsePayload(this._token!);
    if (!payload?.exp) return false;
    return Date.now() / 1000 > payload.exp;
  }

  private decode(token: string, mustChangePassword: boolean): AuthUser | null {
    try {
      const payload = this.parsePayload(token);
      if (!payload) return null;
      return {
        userId: payload.sub,
        email: payload.email,
        role: payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] as UserRole
          ?? payload.role as UserRole,
        mustChangePassword,
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

  private persistSession(mustChangePassword: boolean): void {
    if (!this._token || !this.currentUser()) return;
    sessionStorage.setItem(SessionKey, JSON.stringify({ token: this._token, mustChangePassword }));
  }

  private restoreSession(): void {
    try {
      const raw = sessionStorage.getItem(SessionKey);
      if (!raw) return;
      const session = JSON.parse(raw) as StoredSession;
      this._token = session.token;
      this.currentUser.set(this.decode(session.token, session.mustChangePassword));
      if (!this.isAuthenticated()) this.clear();
    } catch {
      this.clear();
    }
  }
}
