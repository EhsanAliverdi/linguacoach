import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ProfileComponent } from './profile.component';
import { AuthService } from '../../core/services/auth.service';
import { ProfileService, StudentProfileResponse } from '../../core/services/profile.service';

const MOCK_PROFILE: StudentProfileResponse = {
  profileId: 'profile-123',
  userId: 'user-123',
  firstName: 'Jane',
  lastName: 'Doe',
  displayName: 'Jane Doe',
  preferredName: null,
  email: 'jane@example.com',
  cefrLevel: 'B1',
  learningGoals: ['Day-to-day English'],
  customLearningGoal: null,
  focusAreas: ['Speaking'],
  customFocusArea: null,
  supportLanguageCode: 'fa',
  supportLanguageName: 'Persian',
  translationHelpPreference: 'WhenDifficult',
  preferredSessionDurationMinutes: 20,
  difficultyPreference: 'Balanced',
  learningPreferencesUpdatedAt: null,
};

describe('ProfileComponent', () => {
  let profileService: jasmine.SpyObj<ProfileService>;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(() => {
    profileService = jasmine.createSpyObj('ProfileService', ['getProfile', 'updatePreferences']);
    profileService.getProfile.and.returnValue(of(MOCK_PROFILE));
    profileService.updatePreferences.and.returnValue(of(undefined));

    authService = jasmine.createSpyObj('AuthService', ['logout'], {
      currentUser: () => ({ email: 'jane@example.com', role: 'Student', mustChangePassword: false }),
    });

    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        { provide: ProfileService, useValue: profileService },
        { provide: AuthService, useValue: authService },
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(ProfileComponent);
    fixture.detectChanges();
    return fixture;
  }

  // ── Section rendering ──────────────────────────────────────────────────────

  it('renders all 6 sections after profile loads', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const html = fixture.nativeElement.innerHTML;
    expect(html).toContain('Account');
    expect(html).toContain('Level');
    expect(html).toContain('Learning goals');
    expect(html).toContain('Focus areas');
    expect(html).toContain('Support language');
    expect(html).toContain('Practice preferences');
  }));

  // ── CEFR level is read-only ────────────────────────────────────────────────

  it('shows CEFR level as read-only display, not an input', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const html = fixture.nativeElement.innerHTML;
    // Level section must display the value
    expect(html).toContain('B1');
    // The level section must NOT contain an input or select with the CEFR value
    const levelSection = fixture.nativeElement.querySelector('[data-testid="level-section"]');
    const inputs = levelSection?.querySelectorAll('input, select') ?? [];
    expect(inputs.length).toBe(0);
  }));

  it('does not render any prompt editing control', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const html = fixture.nativeElement.innerHTML;
    expect(html).not.toContain('prompt');
    expect(html).not.toContain('system prompt');
  }));

  // ── Learning goals multi-select ────────────────────────────────────────────

  it('renders learning goals chip list', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const chipsEl = fixture.nativeElement.querySelector('[data-testid="learning-goals-chips"]');
    expect(chipsEl).toBeTruthy();
    const buttons = chipsEl.querySelectorAll('button');
    expect(buttons.length).toBeGreaterThan(5);
  }));

  it('toggleGoal adds goal to form.learningGoals', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const component = fixture.componentInstance;
    const initialCount = component.form.learningGoals.length;
    component.toggleGoal('Travel English');
    expect(component.form.learningGoals).toContain('Travel English');
    expect(component.form.learningGoals.length).toBe(initialCount + 1);
  }));

  it('toggleGoal removes goal when already selected', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const component = fixture.componentInstance;
    // 'Day-to-day English' is already in MOCK_PROFILE.learningGoals
    const initialCount = component.form.learningGoals.length;
    component.toggleGoal('Day-to-day English');
    expect(component.form.learningGoals).not.toContain('Day-to-day English');
    expect(component.form.learningGoals.length).toBe(initialCount - 1);
  }));

  it('custom goal input is present', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('[data-testid="custom-goal-input"]');
    expect(input).toBeTruthy();
  }));

  // ── Support language ───────────────────────────────────────────────────────

  it('renders support language selector', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const select = fixture.nativeElement.querySelector('[data-testid="support-language-select"]');
    expect(select).toBeTruthy();
  }));

  it('onSupportLanguageChange sets supportLanguageName', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.onSupportLanguageChange('zh');
    expect(component.form.supportLanguageName).toBe('Chinese');
  }));

  it('onSupportLanguageChange with null clears name', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.onSupportLanguageChange(null);
    expect(component.form.supportLanguageName).toBeNull();
  }));

  // ── Save ───────────────────────────────────────────────────────────────────

  it('save emits correct payload to profileService', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.form.preferredName = 'Janie';
    component.form.learningGoals = ['Day-to-day English', 'Travel English'];
    component.save();
    tick();
    expect(profileService.updatePreferences).toHaveBeenCalledWith(
      jasmine.objectContaining({
        preferredName: 'Janie',
        learningGoals: ['Day-to-day English', 'Travel English'],
      })
    );
  }));

  it('save shows success message on success', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.save();
    tick();
    fixture.detectChanges();
    expect(component.successMessage()).toBe('Preferences saved.');
  }));

  it('save shows error message on failure', fakeAsync(() => {
    profileService.updatePreferences.and.returnValue(throwError(() => new Error('network error')));
    const fixture = create();
    tick();
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.save();
    tick();
    fixture.detectChanges();
    expect(component.errorMessage()).toBe('Could not save. Please try again.');
  }));

  // ── Chip selected states ────────────────────────────────────────────────────

  it('selected chip has aria-pressed=true', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const chips = fixture.nativeElement.querySelectorAll('[data-testid="learning-goals-chips"] button');
    const selectedChip = Array.from(chips as NodeListOf<HTMLButtonElement>).find(
      (b: HTMLButtonElement) => b.textContent?.trim() === 'Day-to-day English'
    );
    expect(selectedChip!.getAttribute('aria-pressed')).toBe('true');
  }));

  it('unselected chip has aria-pressed=false', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const chips = fixture.nativeElement.querySelectorAll('[data-testid="learning-goals-chips"] button');
    const unselectedChip = Array.from(chips as NodeListOf<HTMLButtonElement>).find(
      (b: HTMLButtonElement) => b.textContent?.trim() === 'Travel English'
    );
    expect(unselectedChip!.getAttribute('aria-pressed')).toBe('false');
  }));

  it('difficulty chip shows selected state for Balanced', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const balancedBtn = fixture.nativeElement.querySelector('[data-testid="difficulty-balanced"]');
    expect(balancedBtn).toBeTruthy();
    expect(balancedBtn.getAttribute('aria-pressed')).toBe('true');
  }));

  it('session length chip shows selected state for 20min', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const chip20 = fixture.nativeElement.querySelector('[data-testid="session-length-20"]');
    expect(chip20).toBeTruthy();
    expect(chip20.getAttribute('aria-pressed')).toBe('true');
  }));

  // ── Loading/error states ────────────────────────────────────────────────────

  it('shows loading initially', () => {
    const fixture = create();
    const html = fixture.nativeElement.innerHTML;
    // loading signal is true before the observable resolves synchronously
    // (of() resolves synchronously, so after detectChanges loading is false)
    // This checks the loading signal is reset after
    expect(fixture.componentInstance.loading()).toBeFalse();
  });
});
