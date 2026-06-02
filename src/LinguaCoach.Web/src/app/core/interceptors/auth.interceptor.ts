import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { TokenService } from '../services/token.service';
import { AuthNoticeService } from '../services/auth-notice.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const tokenService = inject(TokenService);
  const notice = inject(AuthNoticeService);
  const router = inject(Router);

  const token = tokenService.getToken();
  if (token) {
    req = req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }

  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401) {
        tokenService.clear();
        notice.set('Your session has expired. Please sign in again.');
        if (!router.url.startsWith('/login')) {
          router.navigate(['/login']);
        }
      } else if (err.status === 403) {
        const message = err.error?.error ?? 'You do not have permission to access that page.';
        notice.set(message);

        if (message.toLowerCase().includes('temporary password')) {
          if (!router.url.startsWith('/change-password')) {
            router.navigate(['/change-password']);
          }
        } else if (message.toLowerCase().includes('onboarding')) {
          if (!router.url.startsWith('/onboarding')) {
            router.navigate(['/onboarding/resume']);
          }
        } else if (!router.url.startsWith('/dashboard')) {
          router.navigate(['/dashboard']);
        }
      }
      return throwError(() => err);
    })
  );
};
