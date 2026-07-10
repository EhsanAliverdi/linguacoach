import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminFeatureGatesComponent } from './admin-feature-gates.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { FeatureGateGroup } from '../../../core/models/admin.models';

// Phase I2C: this mock used to represent "practice-gym-review-scaffold-pilot" (deleted along
// with the readiness pool); rebuilt to represent the surviving "activity-feedback-policy" group
// on the same ReadinessPoolOverride backing store. See
// docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.
const PILOT_GROUP: FeatureGateGroup = {
  groupKey: 'activity-feedback-policy',
  displayName: 'Activity feedback policy',
  description: 'Controls whether students are prompted for feedback after completing an activity.',
  category: 'readinessPoolLessonGeneration',
  isReadOnly: false,
  requiresRestart: false,
  productionChangeAllowed: true,
  dependencies: ['RequireAdminReview should be true'],
  warningText: 'Turning this off is the fastest rollback.',
  settings: [
    {
      key: 'ActivityFeedback.TodayPolicy',
      displayName: 'Today lesson feedback policy',
      description: 'Whether Today-lesson completions prompt the student for feedback.',
      dataType: 'string',
      effectiveValueJson: '"Optional"',
      defaultValueJson: '"Optional"',
      valueSource: 'appSettings',
      isEditableAtRuntime: true,
      isRuntimeEffective: true,
      riskLevel: 'medium',
      requiresConfirmation: false,
      minValue: null,
      maxValue: null,
      maxLength: null,
      allowedValues: ['Off', 'Optional', 'Required'],
    },
    {
      key: 'ActivityFeedback.PracticeGymPolicy',
      displayName: 'Practice Gym feedback policy',
      description: 'Whether Practice Gym completions prompt the student for feedback.',
      dataType: 'string',
      effectiveValueJson: '"Optional"',
      defaultValueJson: '"Optional"',
      valueSource: 'appSettings',
      isEditableAtRuntime: true,
      isRuntimeEffective: true,
      riskLevel: 'low',
      requiresConfirmation: false,
      minValue: null,
      maxValue: null,
      maxLength: null,
      allowedValues: ['Off', 'Optional', 'Required'],
    },
  ],
  lastChangedByUserId: null,
  lastChangedAtUtc: null,
  lastChangeReason: null,
  hasActiveOverride: false,
};

const LOCKED_GROUP: FeatureGateGroup = {
  groupKey: 'ai-signal-safety-speaking',
  displayName: 'AI signal safety — Speaking',
  description: 'Read-only in this phase.',
  category: 'aiSignalSafety',
  isReadOnly: true,
  requiresRestart: true,
  productionChangeAllowed: true,
  dependencies: [],
  warningText: 'These gates default conservative by design.',
  settings: [
    {
      key: 'Speaking.AllowCefrUpdate',
      displayName: 'AI can update CEFR',
      description: 'Always disabled in code.',
      dataType: 'boolean',
      effectiveValueJson: 'false',
      defaultValueJson: 'false',
      valueSource: 'hardcoded',
      isEditableAtRuntime: false,
      isRuntimeEffective: false,
      riskLevel: 'critical',
      requiresConfirmation: false,
      minValue: null,
      maxValue: null,
      maxLength: null,
      allowedValues: null,
    },
  ],
  lastChangedByUserId: null,
  lastChangedAtUtc: null,
  lastChangeReason: null,
  hasActiveOverride: false,
};

const GROUPS: FeatureGateGroup[] = [PILOT_GROUP, LOCKED_GROUP];

function makeApi(groups: FeatureGateGroup[] | 'error' = GROUPS) {
  return {
    getFeatureGates: jasmine.createSpy('getFeatureGates').and.returnValue(
      groups === 'error' ? throwError(() => new Error('fail')) : of(groups),
    ),
    getFeatureGate: jasmine.createSpy('getFeatureGate'),
    updateFeatureGate: jasmine.createSpy('updateFeatureGate').and.returnValue(of(PILOT_GROUP)),
    resetFeatureGateOverride: jasmine.createSpy('resetFeatureGateOverride').and.returnValue(of(PILOT_GROUP)),
  };
}

describe('AdminFeatureGatesComponent', () => {
  let fixture: ComponentFixture<AdminFeatureGatesComponent>;
  let component: AdminFeatureGatesComponent;
  let api: ReturnType<typeof makeApi>;

  async function setup(groups: FeatureGateGroup[] | 'error' = GROUPS, gateParam: string | null = null) {
    api = makeApi(groups);
    await TestBed.configureTestingModule({
      imports: [AdminFeatureGatesComponent],
      providers: [
        provideRouter([]),
        { provide: AdminApiService, useValue: api },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: { queryParamMap: convertToParamMap(gateParam ? { gate: gateParam } : {}) },
          },
        },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminFeatureGatesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('loads feature gates on init', async () => {
    await setup();
    expect(api.getFeatureGates).toHaveBeenCalledTimes(1);
    expect(component.groups().length).toBe(2);
  });

  it('shows a loading state before gates resolve', () => {
    api = makeApi();
    TestBed.configureTestingModule({
      imports: [AdminFeatureGatesComponent],
      providers: [
        provideRouter([]),
        { provide: AdminApiService, useValue: api },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminFeatureGatesComponent);
    expect(fixture.componentInstance.loading()).toBeTrue();
  });

  it('shows an error state when loading fails', async () => {
    await setup('error');
    expect(component.error()).toBeTruthy();
  });

  it('renders category, risk, and status filters', async () => {
    await setup();
    expect(fixture.nativeElement.querySelectorAll('sp-admin-select').length).toBeGreaterThanOrEqual(3);
  });

  it('filters by category', async () => {
    await setup();
    component.categoryFilter.set('aiSignalSafety');
    expect(component.filteredGroups().map((g) => g.groupKey)).toEqual(['ai-signal-safety-speaking']);
  });

  it('filters by search text matching displayName', async () => {
    await setup();
    component.searchText.set('activity feedback');
    expect(component.filteredGroups().map((g) => g.groupKey)).toEqual(['activity-feedback-policy']);
  });

  it('clicking a card opens the drawer', async () => {
    await setup();
    component.openDrawer(PILOT_GROUP);
    expect(component.drawerOpen()).toBeTrue();
    expect(component.selectedGroup()?.groupKey).toBe('activity-feedback-policy');
  });

  it('drawer shows effective value, source, risk, and dependencies for an editable group', async () => {
    await setup();
    component.openDrawer(PILOT_GROUP);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Today lesson feedback policy');
    expect(text).toContain('RequireAdminReview should be true');
    expect(text).toContain('medium risk');
  });

  it('renders editable fields for the activity feedback policy group', async () => {
    await setup();
    component.openDrawer(PILOT_GROUP);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('sp-admin-select').length).toBeGreaterThan(3);
  });

  it('shows a Runtime effective badge for runtime-wired editable settings', async () => {
    await setup();
    component.openDrawer(PILOT_GROUP);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Runtime effective');
  });

  it('locked gates do not show a Save button', async () => {
    await setup();
    component.openDrawer(LOCKED_GROUP);
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('sp-admin-button')) as HTMLElement[];
    expect(buttons.some((b) => b.textContent?.includes('Save'))).toBeFalse();
  });

  it('save requires a reason', async () => {
    await setup();
    component.openDrawer(PILOT_GROUP);
    component.reason.set('');
    component.save();
    expect(api.updateFeatureGate).not.toHaveBeenCalled();
    expect(component.saveError()).toBeTruthy();
  });

  it('save calls the API and refreshes the group', async () => {
    await setup();
    component.openDrawer(PILOT_GROUP);
    component.reason.set('Turning on pilot for a controlled rollout.');
    component.save();
    expect(api.updateFeatureGate).toHaveBeenCalledWith(
      'activity-feedback-policy',
      jasmine.objectContaining({ reason: 'Turning on pilot for a controlled rollout.' }),
    );
  });

  it('reset calls the API and refreshes the group', async () => {
    await setup();
    component.openDrawer(PILOT_GROUP);
    component.reason.set('Rolling back to default.');
    component.resetToDefault();
    expect(api.resetFeatureGateOverride).toHaveBeenCalledWith(
      'activity-feedback-policy',
      { reason: 'Rolling back to default.' },
    );
  });

  it('shows a validation error returned by the API', async () => {
    api = makeApi();
    api.updateFeatureGate.and.returnValue(throwError(() => ({ error: { error: 'Value out of range.' } })));
    await TestBed.configureTestingModule({
      imports: [AdminFeatureGatesComponent],
      providers: [
        provideRouter([]),
        { provide: AdminApiService, useValue: api },
        { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminFeatureGatesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();

    component.openDrawer(PILOT_GROUP);
    component.reason.set('Trying an invalid value.');
    component.save();
    expect(component.saveError()).toBe('Value out of range.');
  });

  it('opens the drawer for the gate named in the ?gate= query param', async () => {
    await setup(GROUPS, 'ai-signal-safety-speaking');
    expect(component.drawerOpen()).toBeTrue();
    expect(component.selectedGroup()?.groupKey).toBe('ai-signal-safety-speaking');
  });
});
