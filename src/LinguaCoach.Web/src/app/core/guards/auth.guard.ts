import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AuthNoticeService } from '../services/auth-notice.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const notice = inject(AuthNoticeService);
  if (!auth.isAuthenticated()) {
    notice.set('Please sign in to continue.');
    return router.createUrlTree(['/login']);
  }
  if (auth.currentUser()?.mustChangePassword && state.url !== '/change-password') {
    notice.set('Please change your temporary password before continuing.');
    return router.createUrlTree(['/change-password']);
  }
  return true;
};
