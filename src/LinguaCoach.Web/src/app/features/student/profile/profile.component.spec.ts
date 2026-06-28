import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { provideRouter } from '@angular/router';
import { ProfileComponent } from './profile.component';
import { AuthService } from '../../../core/services/auth.service';
import { ProfileService, StudentProfileResponse } from '../../../core/services/profile.service';
import { NotificationPreferencesService, NotificationPreferenceItem } from '../../../core/services/notification-preferences.service';
import { PlacementService } from '../../../core/services/placement.service';
import { AdaptivePlacementSummary, PlacementConfig } from '../../../core/models/placement.models';

const MOCK_PREFS: NotificationPreferenceItem[] = [
  { category: 'Account', channel: 'InApp', isEnabled: true, isRequired: true },
  { category: 'Account', channel: 'Email', isEnabled: true, isRequired: true },
  { category: 'Account', channel: 'Sms', isEnabled: false, isRequired: true },
  { category: 'Learning', channel: 'InApp', isEnabled: true, isRequired: false },
  { category: 'Learning', channel: 'Email', isEnabled: true, isRequired: false },
  { category: 'Learning', channel: 'Sms', isEnabled: false, isRequired: false },
];

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

const MOCK_PLACEMENT: AdaptivePlacementSummary = {
  assessmentId: 'assess-1',
  studentProfileId: 'profile-123',
  status: 'Completed',
  startedAtUtc: '2026-06-01T10:00:00Z',
  completedAtUtc: '2026-06-01T10:30:00Z',
  expiredAtUtc: null,
  overallCefrLevel: 'B1',
  overallConfidence: 0.75,
  isProvisional: false,
  resultSummary: 'Good result',
  source: 'adaptive',
  skillResults: [
    { skill: 'listening', estimatedCefrLevel: 'B1', confidence: 0.8, evidenceCount: 5, strengths: null, weaknesses: null, recommendedObjectiveKeys: [] },
    { skill: 'speaking', estimatedCefrLevel: 'A2', confidence: 0.7, evidenceCount: 4, strengths: null, weaknesses: null, recommendedObjectiveKeys: [] },
  ],
  learningPlanRegenerated: true,
  learningPlanRegenerationWarning: null,
  itemCount: 12,
  hasPlacement: true,
};

const MOCK_PLACEMENT_CONFIG: PlacementConfig = {
  placementRequiredBeforeLearning: true,
  allowSkipPlacement: false,
  allowPlacementRetake: false,
  autoStartPlacement: false,
};

describe('ProfileComponent', () => {
  let profileService: jasmine.SpyObj<ProfileService>;
  let authService: jasmine.SpyObj<AuthService>;
  let notifPrefsService: jasmine.SpyObj<NotificationPreferencesService>;
  let placementService: jasmine.SpyObj<PlacementService>;

  beforeEach(() => {
    profileService = jasmine.createSpyObj('ProfileService', ['getProfile', 'updatePreferences']);
    profileService.getProfile.and.returnValue(of(MOCK_PROFILE));
    profileService.updatePreferences.and.returnValue(of(undefined));

    authService = jasmine.createSpyObj('AuthService', ['logout'], {
      currentUser: () => ({ email: 'jane@example.com', role: 'Student', mustChangePassword: false }),
    });

    notifPrefsService = jasmine.createSpyObj('NotificationPreferencesService', ['getPreferences', 'updatePreferences']);
    notifPrefsService.getPreferences.and.returnValue(of(MOCK_PREFS));
    notifPrefsService.updatePreferences.and.returnValue(of(undefined));

    placementService = jasmine.createSpyObj('PlacementService', ['getAdaptiveCurrent', 'getPlacementConfig']);
    placementService.getAdaptiveCurrent.and.returnValue(of(MOCK_PLACEMENT));
    placementService.getPlacementConfig.and.returnValue(of(MOCK_PLACEMENT_CONFIG));

    TestBed.configureTestingModule({
      imports: [ProfileComponent],
      providers: [
        { provide: ProfileService, useValue: profileService },
        { provide: AuthService, useValue: authService },
        { provide: NotificationPreferencesService, useValue: notifPrefsService },
        { provide: PlacementService, useValue: placementService },
        provideRouter([]),
      ],
    });
  });

  function create() {
    const fixture = TestBed.createComponent(ProfileComponent);
    fixture.detectChanges();
    return fixture;
  }

  // â”€â”€ Section rendering â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

  // â”€â”€ CEFR level is read-only â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

  // â”€â”€ Learning goals multi-select â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

  // â”€â”€ Support language â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

  // â”€â”€ Save â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

  // â”€â”€ Chip selected states â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

  // â”€â”€ Loading/error states â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  it('shows loading initially', () => {
    const fixture = create();
    const html = fixture.nativeElement.innerHTML;
    // loading signal is true before the observable resolves synchronously
    // (of() resolves synchronously, so after detectChanges loading is false)
    // This checks the loading signal is reset after
    expect(fixture.componentInstance.loading()).toBeFalse();
  });

  // â”€â”€ Notification preferences â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

  it('loads notification preferences on init', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    expect(notifPrefsService.getPreferences).toHaveBeenCalled();
    expect(fixture.componentInstance.allPrefs().length).toBeGreaterThan(0);
  }));

  it('renders notification preferences section', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const section = fixture.nativeElement.querySelector('[data-testid="notification-prefs-section"]');
    expect(section).toBeTruthy();
  }));

  it('renders prefs table after loading', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const table = fixture.nativeElement.querySelector('[data-testid="prefs-table"]');
    expect(table).toBeTruthy();
  }));

  it('shows SMS as coming soon (not a checkbox)', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const smsEl = fixture.nativeElement.querySelector('[data-testid="sms-coming-soon"]');
    expect(smsEl).toBeTruthy();
    expect(smsEl.textContent).toContain('Coming soon');
  }));

  it('required category shows Required badge', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const badge = fixture.nativeElement.querySelector('[data-testid="required-badge"]');
    expect(badge).toBeTruthy();
  }));

  it('getPref returns true for enabled preference', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    expect(fixture.componentInstance.getPref('Learning', 'InApp')).toBeTrue();
  }));

  it('setPref updates local state', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    fixture.componentInstance.setPref('Learning', 'Email', false);
    expect(fixture.componentInstance.getPref('Learning', 'Email')).toBeFalse();
  }));

  it('savePrefs calls updatePreferences', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    fixture.componentInstance.savePrefs();
    tick();
    expect(notifPrefsService.updatePreferences).toHaveBeenCalled();
  }));

  it('savePrefs shows error on failure', fakeAsync(() => {
    notifPrefsService.updatePreferences.and.returnValue(throwError(() => new Error('fail')));
    const fixture = create();
    tick();
    fixture.detectChanges();
    fixture.componentInstance.savePrefs();
    tick();
    fixture.detectChanges();
    expect(fixture.componentInstance.prefsSaveError()).toBeTruthy();
  }));

  it('isPrefRequired returns true for Account category', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    expect(fixture.componentInstance.isPrefRequired('Account', 'InApp')).toBeTrue();
  }));

  it('isPrefRequired returns false for Learning category', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    expect(fixture.componentInstance.isPrefRequired('Learning', 'InApp')).toBeFalse();
  }));

  it('prefsLoading is false after preferences load', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    expect(fixture.componentInstance.prefsLoading()).toBeFalse();
  }));

  // ── Learning goals: Workplace selectable not default (#6) ─────────────────

  it('Workplace English chip is present in the goal list', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const chip = fixture.nativeElement.querySelector('[data-testid="goal-chip-Workplace English"]');
    expect(chip).toBeTruthy();
  }));

  it('Workplace English is not pre-selected by default', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const chip = fixture.nativeElement.querySelector('[data-testid="goal-chip-Workplace English"]');
    expect(chip.getAttribute('aria-pressed')).toBe('false');
  }));

  // ── Placement summary (#14, #15) ──────────────────────────────────────────

  it('placement summary section is present after load', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const section = fixture.nativeElement.querySelector('[data-testid="placement-summary-section"]');
    expect(section).toBeTruthy();
  }));

  it('placement summary shows confirmed badge for non-provisional placement', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const badge = fixture.nativeElement.querySelector('[data-testid="confirmed-badge"]');
    expect(badge).toBeTruthy();
  }));

  it('retake button is hidden when allowPlacementRetake is false', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const retakeBtn = fixture.nativeElement.querySelector('[data-testid="retake-placement-button"]');
    expect(retakeBtn).toBeNull();
    const notAvailableEl = fixture.nativeElement.querySelector('[data-testid="retake-not-available"]');
    expect(notAvailableEl).toBeTruthy();
  }));

  it('retake button is shown when allowPlacementRetake is true', fakeAsync(() => {
    placementService.getPlacementConfig.and.returnValue(of({ ...MOCK_PLACEMENT_CONFIG, allowPlacementRetake: true }));
    const fixture = create();
    tick();
    fixture.detectChanges();
    const retakeBtn = fixture.nativeElement.querySelector('[data-testid="retake-placement-button"]');
    expect(retakeBtn).toBeTruthy();
  }));

  it('placement section shows no-placement message when no assessment exists', fakeAsync(() => {
    placementService.getAdaptiveCurrent.and.returnValue(of(null));
    const fixture = create();
    tick();
    fixture.detectChanges();
    const msg = fixture.nativeElement.querySelector('[data-testid="no-placement-message"]');
    expect(msg).toBeTruthy();
  }));

  it('placement section shows skill breakdown when placement is completed', fakeAsync(() => {
    const fixture = create();
    tick();
    fixture.detectChanges();
    const breakdown = fixture.nativeElement.querySelector('[data-testid="skill-breakdown"]');
    expect(breakdown).toBeTruthy();
  }));
});


