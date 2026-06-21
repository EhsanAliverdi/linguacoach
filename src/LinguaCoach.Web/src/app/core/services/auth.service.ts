import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs/operators';
import { Observable } from 'rxjs';
import {
  LoginRequest, LoginResponse, ChangePasswordRequest, ResetPasswordRequest
} from '../models/auth.models';
import { TokenService } from './token.service';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = environment.apiUrl;
  readonly currentUser: TokenService['currentUser'];

  constructor(private http: HttpClient, private tokenService: TokenService, private router: Router) {
    this.currentUser = tokenService.currentUser;
  }

  login(request: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>(`${this.api}/auth/login`, request).pipe(
      tap(res => this.tokenService.setToken(res.token, res.mustChangePassword))
    );
  }

  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.api}/auth/change-password`, request).pipe(
      tap(() => this.tokenService.setMustChangePassword(false))
    );
  }

  resetPassword(request: ResetPasswordRequest): Observable<void> {
    return this.http.post<void>(`${this.api}/auth/reset-password`, request);
  }

  logout(): void {
    this.tokenService.clear();
    this.router.navigate(['/login']);
  }

  isAuthenticated(): boolean {
    return this.tokenService.isAuthenticated();
  }

  getToken(): string | null {
    return this.tokenService.getToken();
  }
}
