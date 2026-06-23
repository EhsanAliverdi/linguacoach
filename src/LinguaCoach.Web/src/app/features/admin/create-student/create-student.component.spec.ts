import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CreateStudentComponent } from './create-student.component';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { ToastService } from '../../../core/services/toast.service';
import { Router } from '@angular/router';

function makeAdminService(succeed = true) {
  return {
    createStudent: jasmine.createSpy('createStudent').and.returnValue(
      succeed ? of({}) : throwError(() => ({ status: 500, error: { error: 'Server error' } })),
    ),
  };
}

function makeAuthService() {
  return { currentUser: null };
}

function makeToast() {
  return { success: jasmine.createSpy('success'), error: jasmine.createSpy('error') };
}

describe('CreateStudentComponent', () => {
  let fixture: ComponentFixture<CreateStudentComponent>;
  let component: CreateStudentComponent;
  let adminSvc: ReturnType<typeof makeAdminService>;
  let toast: ReturnType<typeof makeToast>;

  async function setup(succeed = true) {
    adminSvc = makeAdminService(succeed);
    toast = makeToast();
    await TestBed.configureTestingModule({
      imports: [CreateStudentComponent],
      providers: [
        provideRouter([]),
        { provide: AdminService, useValue: adminSvc },
        { provide: AuthService, useValue: makeAuthService() },
        { provide: ToastService, useValue: toast },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(CreateStudentComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the page title', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Create student');
  });

  it('renders page header wrapper', async () => {
    await setup();
    expect(fixture.nativeElement.querySelector('sp-admin-page-header')).toBeTruthy();
  });

  it('renders page body wrapper', async () => {
    await setup();
    expect(fixture.nativeElement.querySelector('sp-admin-page-body')).toBeTruthy();
  });

  it('renders card wrapper or section container', async () => {
    await setup();
    const hasCard = !!fixture.nativeElement.querySelector('sp-admin-card');
    const hasSection = fixture.nativeElement.textContent.includes('Account credentials');
    expect(hasCard || hasSection).toBeTrue();
  });

  it('renders email form field', async () => {
    await setup();
    const text: string = fixture.nativeElement.textContent;
    expect(text).toContain('Student email');
  });

  it('renders temporary password form field', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Temporary password');
  });

  it('renders require password change toggle', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Require password change on first login');
  });

  it('renders optional profile fields toggle', async () => {
    await setup();
    const text: string = fixture.nativeElement.textContent;
    expect(text.includes('Add optional profile fields') || text.includes('Add profile fields')).toBeTrue();
  });

  it('renders submit button', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Create student');
  });

  it('shows email validation error when email is empty', async () => {
    await setup();
    component.email = '';
    component.temporaryPassword = 'ValidPass1!';
    component.onSubmit();
    fixture.detectChanges();
    expect(component.emailError()).toContain('email');
  });

  it('shows password validation error when password is too short', async () => {
    await setup();
    component.email = 'test@example.com';
    component.temporaryPassword = 'short';
    component.onSubmit();
    fixture.detectChanges();
    expect(component.passwordError()).toBeTruthy();
  });

  it('shows password validation error when password has no digit', async () => {
    await setup();
    component.email = 'test@example.com';
    component.temporaryPassword = 'NoDigitHere!';
    component.onSubmit();
    fixture.detectChanges();
    expect(component.passwordError()).toBeTruthy();
  });

  it('calls createStudent with correct payload on valid submit', async () => {
    await setup();
    component.email = 'alice@example.com';
    component.temporaryPassword = 'ValidPass1!';
    component.mustChangePassword = true;
    component.onSubmit();
    fixture.detectChanges();
    expect(adminSvc.createStudent).toHaveBeenCalledWith(jasmine.objectContaining({
      email: 'alice@example.com',
      temporaryPassword: 'ValidPass1!',
      mustChangePassword: true,
    }));
  });

  it('does not include optional fields when showOptional is false', async () => {
    await setup();
    component.email = 'alice@example.com';
    component.temporaryPassword = 'ValidPass1!';
    component.showOptional = false;
    component.firstName = 'Alice';
    component.onSubmit();
    const payload = adminSvc.createStudent.calls.mostRecent().args[0];
    expect(payload.firstName).toBeUndefined();
  });

  it('includes optional fields when showOptional is true and values provided', async () => {
    await setup();
    component.email = 'alice@example.com';
    component.temporaryPassword = 'ValidPass1!';
    component.showOptional = true;
    component.firstName = 'Alice';
    component.learningGoal = 'Meetings';
    component.onSubmit();
    const payload = adminSvc.createStudent.calls.mostRecent().args[0];
    expect(payload.firstName).toBe('Alice');
    expect(payload.learningGoal).toBe('Meetings');
  });

  it('shows server error message on 500', async () => {
    await setup(false);
    component.email = 'alice@example.com';
    component.temporaryPassword = 'ValidPass1!';
    component.onSubmit();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.error()).toContain('Server error');
  });

  it('shows conflict error on 409', async () => {
    adminSvc.createStudent.and.returnValue(throwError(() => ({ status: 409 })));
    component.email = 'alice@example.com';
    component.temporaryPassword = 'ValidPass1!';
    component.onSubmit();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.error()).toContain('already exists');
  });

  it('preserves temp password security wording', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('temporary password');
  });

  // ── REDESIGN-2 new tests ──────────────────────────────────────────────────

  describe('page structure', () => {
    it('renders back-to-students button', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Back to Students');
    });

    it('renders Account credentials section heading', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Account credentials');
    });

    it('renders Student profile section heading', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Student profile');
    });

    it('renders optional badge on profile section', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Optional');
    });

    it('renders what-happens-next aside panel', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('What happens next');
    });

    it('aside panel lists onboarding steps', async () => {
      await setup();
      const text: string = fixture.nativeElement.textContent;
      expect(text).toContain('Onboarding');
      expect(text).toContain('Placement test');
    });
  });

  describe('security note', () => {
    it('renders security note about one-time password', async () => {
      await setup();
      const text: string = fixture.nativeElement.textContent;
      expect(text).toContain('temporary password');
      expect(text).toContain('once');
    });

    it('renders welcome email not-available-yet note', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Welcome email');
    });

    it('does not expose any API key or secret', async () => {
      await setup();
      const text: string = fixture.nativeElement.textContent;
      expect(text).not.toContain('API_KEY');
      expect(text).not.toContain('sk-');
      expect(text).not.toContain('JWT_KEY');
    });
  });

  describe('profile toggle', () => {
    it('optional profile fields are hidden by default', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).not.toContain('First name');
    });

    it('optional fields appear after toggling showOptional', async () => {
      await setup();
      component.showOptional = true;
      fixture.detectChanges();
      expect(fixture.nativeElement.textContent).toContain('First name');
      expect(fixture.nativeElement.textContent).toContain('Career context');
    });
  });

  describe('cancel action', () => {
    it('renders cancel button', async () => {
      await setup();
      expect(fixture.nativeElement.textContent).toContain('Cancel');
    });
  });
});
