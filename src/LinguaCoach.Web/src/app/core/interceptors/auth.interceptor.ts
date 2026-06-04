import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { TokenService } from '../services/token.service';
import { AuthNoticeService } from '../services/auth-notice.service';

/** Generate a short 12-char correlation ID (hex). */
function newCorrelationId(): string {
  return Array.from(crypto.getRandomValues(new Uint8Array(6)))
    .map(b => b.toString(16).padStart(2, '0'))
    .join('');
}

/** Extract correlationId from API error response body or response header. */
function extractCorrelationId(err: HttpErrorResponse): string | null {
  return err.error?.correlationId
    ?? err.headers?.get('x-correlation-id')
    ?? null;
}

/** Build a user-friendly error message that includes the reference ID when available. */
function friendlyMessage(err: HttpErrorResponse): string {
  const cid = extractCorrelationId(err);
  const base = err.error?.message
    ?? err.error?.error
    ?? 'Something went wrong. Please try again.';

  return cid ? `${base}\nReference: ${cid}` : base;
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const tokenService = inject(TokenService);
  const notice = inject(AuthNoticeService);
  const router = inject(Router);

  // Attach JWT if present
  let outReq = req;
  const token = tokenService.getToken();
  if (token) {
    outReq = outReq.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }

  // Attach correlation ID (use existing or generate new)
  const existingCid = req.headers.get('X-Correlation-ID');
  if (!existingCid) {
    outReq = outReq.clone({ setHeaders: { 'X-Correlation-ID': newCorrelationId() } });
  }

  return next(outReq).pipe(
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
      } else if (err.status >= 500) {
        // For server errors, log to console in dev and preserve correlationId for display
        const cid = extractCorrelationId(err);
        if (cid) {
          console.warn(`[SpeakPath] Server error on ${req.method} ${req.url}. Reference: ${cid}`);
        }
      }
      return throwError(() => err);
    })
  );
};
