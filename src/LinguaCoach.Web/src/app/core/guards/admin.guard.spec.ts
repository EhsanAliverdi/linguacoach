import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { signal } from '@angular/core';
import { adminGuard } from './admin.guard';
import { AuthService } from '../services/auth.service';
import { AuthUser } from '../models/auth.models';
import { AuthNoticeService } from '../services/auth-notice.service';

describe('adminGuard', () => {
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;
  let authNotice: jasmine.SpyObj<AuthNoticeService>;

  function makeAuthService(isAuth: boolean, user: AuthUser | null) {
    authService = jasmine.createSpyObj('AuthService', ['isAuthenticated']);
    authService.isAuthenticated.and.returnValue(isAuth);
    (authService as any).currentUser = signal(user);
    return authService;
  }

  beforeEach(() => {
    router = jasmine.createSpyObj('Router', ['createUrlTree', 'navigate']);
    router.createUrlTree.and.returnValue({ toString: () => '/login' } as any);
    authNotice = jasmine.createSpyObj('AuthNoticeService', ['set']);
  });

  it('returns true for authenticated Admin', () => {
    const svc = makeAuthService(true, { userId: '1', email: 'a@b.com', role: 'Admin', mustChangePassword: false });
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: svc }, { provide: Router, useValue: router }, { provide: AuthNoticeService, useValue: authNotice }] });
    const result = TestBed.runInInjectionContext(() => adminGuard({} as any, {} as any));
    expect(result).toBeTrue();
  });

  it('redirects Student to /dashboard', () => {
    const svc = makeAuthService(true, { userId: '2', email: 's@b.com', role: 'Student', mustChangePassword: false });
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: svc }, { provide: Router, useValue: router }, { provide: AuthNoticeService, useValue: authNotice }] });
    TestBed.runInInjectionContext(() => adminGuard({} as any, {} as any));
    expect(router.createUrlTree).toHaveBeenCalledWith(['/dashboard']);
    expect(authNotice.set).toHaveBeenCalledWith('You do not have permission to use the admin area.');
  });

  it('redirects unauthenticated user to /login', () => {
    const svc = makeAuthService(false, null);
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: svc }, { provide: Router, useValue: router }, { provide: AuthNoticeService, useValue: authNotice }] });
    TestBed.runInInjectionContext(() => adminGuard({} as any, {} as any));
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
    expect(authNotice.set).toHaveBeenCalledWith('Please sign in with an admin account to continue.');
  });
});
