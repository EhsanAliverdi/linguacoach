import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAuthenticated() && auth.currentUser()?.mustChangePassword) {
    return router.createUrlTree(['/change-password']);
  }
  if (auth.isAuthenticated() && auth.currentUser()?.role === 'Admin') return true;
  return router.createUrlTree(['/login']);
};
