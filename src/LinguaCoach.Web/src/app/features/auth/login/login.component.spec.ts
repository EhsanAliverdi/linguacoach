import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LoginComponent } from './login.component';
import { AuthService } from '../../../core/services/auth.service';
import { OnboardingService } from '../../../core/services/onboarding.service';
import { AuthNoticeService } from '../../../core/services/auth-notice.service';

describe('LoginComponent', () => {
  let authService: jasmine.SpyObj<AuthService>;
  let onboardingService: jasmine.SpyObj<OnboardingService>;
  let router: jasmine.SpyObj<Router>;
  let authNotice: jasmine.SpyObj<AuthNoticeService>;

  beforeEach(() => {
    authService = jasmine.createSpyObj('AuthService', ['login']);
    onboardingService = jasmine.createSpyObj('OnboardingService', ['getStatus']);
    router = jasmine.createSpyObj('Router', ['navigate', 'createUrlTree', 'serializeUrl']);
    (router as any).events = of(null);
    router.createUrlTree.and.returnValue({} as any);
    router.serializeUrl.and.returnValue('');
    authNotice = jasmine.createSpyObj('AuthNoticeService', ['consume']);
    authNotice.consume.and.returnValue(null);

    TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: { get: () => null } } } },
        { provide: AuthService, useValue: authService },
        { provide: OnboardingService, useValue: onboardingService },
        { provide: Router, useValue: router },
        { provide: AuthNoticeService, useValue: authNotice },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(LoginComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('sets error signal on failed login', fakeAsync(() => {
    authService.login.and.returnValue(throwError(() => ({ error: { error: 'Invalid credentials.' } })));
    const fixture = create();
    const component = fixture.componentInstance;
    component.email = 'test@test.com';
    component.password = 'wrong';
    component.onSubmit();
    tick();
    expect(component.error()).toBe('Invalid credentials.');
    expect(router.navigate).not.toHaveBeenCalled();
  }));

  it('routes mustChangePassword user to /change-password', fakeAsync(() => {
    authService.login.and.returnValue(of({ token: 'tok', role: 'Student', mustChangePassword: true }));
    const fixture = create();
    const component = fixture.componentInstance;
    component.email = 'test@test.com';
    component.password = 'pass';
    component.onSubmit();
    tick();
    expect(router.navigate).toHaveBeenCalledWith(['/change-password']);
  }));

  it('routes Admin to /admin', fakeAsync(() => {
    authService.login.and.returnValue(of({ token: 'tok', role: 'Admin', mustChangePassword: false }));
    const fixture = create();
    const component = fixture.componentInstance;
    component.email = 'admin@test.com';
    component.password = 'pass';
    component.onSubmit();
    tick();
    expect(router.navigate).toHaveBeenCalledWith(['/admin']);
  }));

  it('routes Student with complete onboarding to /dashboard', fakeAsync(() => {
    authService.login.and.returnValue(of({ token: 'tok', role: 'Student', mustChangePassword: false }));
    onboardingService.getStatus.and.returnValue(of({ currentStep: 'Skill', isComplete: true }));
    const fixture = create();
    const component = fixture.componentInstance;
    component.email = 'student@test.com';
    component.password = 'pass';
    component.onSubmit();
    tick();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  }));

  it('does not submit when email or password is empty', fakeAsync(() => {
    const fixture = create();
    const component = fixture.componentInstance;
    component.email = '';
    component.password = '';
    component.onSubmit();
    tick();
    expect(authService.login).not.toHaveBeenCalled();
  }));

  it('shows a consumed auth notice', () => {
    authNotice.consume.and.returnValue('Your session has expired. Please sign in again.');
    const fixture = create();
    expect(fixture.componentInstance.notice()).toBe('Your session has expired. Please sign in again.');
  });
});
