import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { Observable } from 'rxjs';
import {
  LoginRequest, LoginResponse, ChangePasswordRequest, AuthUser
} from '../models/auth.models';
import { TokenService } from './token.service';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = environment.apiUrl;

  // Signal-based current user — components subscribe reactively
  readonly currentUser = signal<AuthUser | null>(null);

  constructor(private http: HttpClient, private tokenService: TokenService, private router: Router) {}

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.api}/auth/login`, request).pipe(
      tap(res => {
        this.tokenService.setToken(res.token);
        const user = this.tokenService.getUser();
        if (user) {
          user.mustChangePassword = res.mustChangePassword;
          this.currentUser.set(user);
        }
      })
    );
  }

  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.api}/auth/change-password`, request).pipe(
      tap(() => {
        const user = this.currentUser();
        if (user) this.currentUser.set({ ...user, mustChangePassword: false });
      })
    );
  }

  logout(): void {
    this.tokenService.clear();
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }

  isAuthenticated(): boolean {
    return this.tokenService.isAuthenticated();
  }

  getToken(): string | null {
    return this.tokenService.getToken();
  }
}
