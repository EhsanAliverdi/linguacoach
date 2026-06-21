import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ResetPasswordComponent } from './reset-password.component';
import { AuthService } from '../../../core/services/auth.service';

function makeRoute(params: Record<string, string>) {
  return {
    snapshot: {
      queryParamMap: {
        get: (key: string) => params[key] ?? null,
      },
    },
  };
}

describe('ResetPasswordComponent', () => {
  let fixture: ComponentFixture<ResetPasswordComponent>;
  let component: ResetPasswordComponent;
  let authSpy: jasmine.SpyObj<AuthService>;

  function setup(queryParams: Record<string, string> = { userId: 'u1', token: 'tok' }) {
    authSpy = jasmine.createSpyObj<AuthService>('AuthService', ['resetPassword']);

    TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authSpy },
        { provide: ActivatedRoute, useValue: makeRoute(queryParams) },
      ],
    });

    fixture = TestBed.createComponent(ResetPasswordComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  }

  it('renders the form when params are valid', () => {
    setup();
    expect(component.error()).toBe('');
    expect(component.success()).toBeFalse();
  });

  it('sets error when userId or token missing', () => {
    setup({ userId: '', token: '' });
    expect(component.error()).toContain('invalid or has expired');
  });

  it('sets error when passwords do not match', () => {
    setup();
    component.newPassword = 'abcdefgh';
    component.confirmPassword = 'XXXXXXXX';
    component.onSubmit();
    expect(component.error()).toContain('do not match');
    expect(authSpy.resetPassword).not.toHaveBeenCalled();
  });

  it('sets error when password too short', () => {
    setup();
    component.newPassword = 'abc';
    component.confirmPassword = 'abc';
    component.onSubmit();
    expect(component.error()).toContain('at least 8');
    expect(authSpy.resetPassword).not.toHaveBeenCalled();
  });

  it('calls auth.resetPassword with correct payload on valid submit', () => {
    setup();
    authSpy.resetPassword.and.returnValue(of(undefined));
    component.newPassword = 'StrongPass1!';
    component.confirmPassword = 'StrongPass1!';
    component.onSubmit();
    expect(authSpy.resetPassword).toHaveBeenCalledWith({
      userId: 'u1',
      token: 'tok',
      newPassword: 'StrongPass1!',
      confirmPassword: 'StrongPass1!',
    });
  });

  it('sets success on successful reset', () => {
    setup();
    authSpy.resetPassword.and.returnValue(of(undefined));
    component.newPassword = 'StrongPass1!';
    component.confirmPassword = 'StrongPass1!';
    component.onSubmit();
    expect(component.success()).toBeTrue();
    expect(component.error()).toBe('');
  });

  it('sets error on failed reset', () => {
    setup();
    authSpy.resetPassword.and.returnValue(throwError(() => ({ error: { error: 'Link expired.' } })));
    component.newPassword = 'StrongPass1!';
    component.confirmPassword = 'StrongPass1!';
    component.onSubmit();
    expect(component.success()).toBeFalse();
    expect(component.error()).toContain('Link expired.');
  });
});
