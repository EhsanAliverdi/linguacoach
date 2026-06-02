import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AuthNoticeService } from '../services/auth-notice.service';

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const notice = inject(AuthNoticeService);
  if (auth.isAuthenticated() && auth.currentUser()?.mustChangePassword) {
    notice.set('Please change your temporary password before continuing.');
    return router.createUrlTree(['/change-password']);
  }
  if (auth.isAuthenticated() && auth.currentUser()?.role === 'Admin') return true;
  if (auth.isAuthenticated()) {
    notice.set('You do not have permission to use the admin area.');
    return router.createUrlTree(['/dashboard']);
  }
  notice.set('Please sign in with an admin account to continue.');
  return router.createUrlTree(['/login']);
};
