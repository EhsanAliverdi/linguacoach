import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { signal } from '@angular/core';
import { adminGuard } from './admin.guard';
import { AuthService } from '../services/auth.service';
import { AuthUser } from '../models/auth.models';

describe('adminGuard', () => {
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  function makeAuthService(isAuth: boolean, user: AuthUser | null) {
    authService = jasmine.createSpyObj('AuthService', ['isAuthenticated']);
    authService.isAuthenticated.and.returnValue(isAuth);
    (authService as any).currentUser = signal(user);
    return authService;
  }

  beforeEach(() => {
    router = jasmine.createSpyObj('Router', ['createUrlTree', 'navigate']);
    router.createUrlTree.and.returnValue({ toString: () => '/login' } as any);
  });

  it('returns true for authenticated Admin', () => {
    const svc = makeAuthService(true, { userId: '1', email: 'a@b.com', role: 'Admin', mustChangePassword: false });
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: svc }, { provide: Router, useValue: router }] });
    const result = TestBed.runInInjectionContext(() => adminGuard({} as any, {} as any));
    expect(result).toBeTrue();
  });

  it('redirects Student to /login', () => {
    const svc = makeAuthService(true, { userId: '2', email: 's@b.com', role: 'Student', mustChangePassword: false });
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: svc }, { provide: Router, useValue: router }] });
    TestBed.runInInjectionContext(() => adminGuard({} as any, {} as any));
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
  });

  it('redirects unauthenticated user to /login', () => {
    const svc = makeAuthService(false, null);
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: svc }, { provide: Router, useValue: router }] });
    TestBed.runInInjectionContext(() => adminGuard({} as any, {} as any));
    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
  });
});
