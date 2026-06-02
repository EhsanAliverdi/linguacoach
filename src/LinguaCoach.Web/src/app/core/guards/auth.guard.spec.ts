import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { signal } from '@angular/core';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';

describe('authGuard', () => {
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    authService = jasmine.createSpyObj('AuthService', ['isAuthenticated']);
    (authService as any).currentUser = signal(null);
    router = jasmine.createSpyObj('Router', ['createUrlTree', 'navigate']);
    router.createUrlTree.and.returnValue({ toString: () => '/login' } as any);

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router },
      ],
    });
  });

  it('returns true when authenticated', () => {
    authService.isAuthenticated.and.returnValue(true);
    const result = TestBed.runInInjectionContext(() => authGuard({} as any, {} as any));
    expect(result).toBeTrue();
  });

  it('redirects to /login when not authenticated', () => {
    authService.isAuthenticated.and.returnValue(false);
    TestBed.runInInjectionContext(() => authGuard({} as any, {} as any));
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
  });

  it('redirects temporary-password user to /change-password', () => {
    authService.isAuthenticated.and.returnValue(true);
    (authService as any).currentUser = signal({
      userId: '1', email: 'student@example.com', role: 'Student', mustChangePassword: true
    });

    TestBed.runInInjectionContext(() => authGuard({} as any, { url: '/dashboard' } as any));

    expect(router.createUrlTree).toHaveBeenCalledWith(['/change-password']);
  });
});
