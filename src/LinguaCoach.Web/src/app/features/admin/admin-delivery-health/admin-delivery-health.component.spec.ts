import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminDeliveryHealthComponent } from './admin-delivery-health.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AdminDeliveryHealth, MasteryValidationSummary } from '../../../core/models/admin.models';

// Rehaul (2026-07-17): replaces the deleted admin-today-delivery-health.component.spec.ts, which
// tested the now-deleted lesson-generation-buffer/settings/batches UI. See
// docs/reviews/2026-07-17-today-delivery-health-bank-first-rehaul-review.md.

const TODAY_HEALTH: AdminDeliveryHealth = {
  today: { eligibleStudents: 10, selectedCount: 7, fallbackOnlyCount: 2, noAssignmentCount: 1 },
  byCefrLevel: [
    { cefrLevel: 'A1', eligibleStudents: 4, selectedCount: 3, fallbackOnlyCount: 1 },
    { cefrLevel: 'B1', eligibleStudents: 6, selectedCount: 4, fallbackOnlyCount: 1 },
  ],
  trend: Array.from({ length: 7 }, (_, i) => ({ date: `2026-07-${10 + i}`, selectedCount: 5, fallbackOnlyCount: 1 })),
  topFallbackReasons: [{ reason: 'No approved module for CEFR level A1', count: 2 }],
  bankCoverage: [
    { cefrLevel: 'A1', eligibleStudents: 4, approvedModuleCount: 0 },
    { cefrLevel: 'B1', eligibleStudents: 6, approvedModuleCount: 3 },
  ],
};

const PRACTICE_GYM_HEALTH: AdminDeliveryHealth = {
  today: { eligibleStudents: 8, selectedCount: 5, fallbackOnlyCount: 1, noAssignmentCount: 2 },
  byCefrLevel: [{ cefrLevel: 'B1', eligibleStudents: 8, selectedCount: 5, fallbackOnlyCount: 1 }],
  trend: Array.from({ length: 7 }, (_, i) => ({ date: `2026-07-${10 + i}`, selectedCount: 3, fallbackOnlyCount: 0 })),
  topFallbackReasons: [],
  bankCoverage: [{ cefrLevel: 'B1', eligibleStudents: 8, approvedModuleCount: 2 }],
};

const MASTERY_VALIDATION: MasteryValidationSummary = {
  totalStudentsEvaluated: 0,
  totalObjectivesEvaluated: 0,
  countInsufficientEvidence: 0,
  countMastered: 0,
  countNeedsReview: 0,
  countNeedsPractice: 0,
  countAtRisk: 0,
  masteredExcludedFromNewLearning: 0,
  warnings: [],
  generatedAt: '2026-06-27T00:00:00Z',
};

function makeApi(
  todayHealth: AdminDeliveryHealth | 'error' = TODAY_HEALTH,
  practiceGymHealth: AdminDeliveryHealth | 'error' = PRACTICE_GYM_HEALTH,
  mastery: MasteryValidationSummary | 'error' = MASTERY_VALIDATION,
) {
  return {
    getTodayPlanDeliveryHealth: jasmine.createSpy('getTodayPlanDeliveryHealth').and.returnValue(
      todayHealth === 'error' ? throwError(() => new Error('fail')) : of(todayHealth),
    ),
    getPracticeGymDeliveryHealth: jasmine.createSpy('getPracticeGymDeliveryHealth').and.returnValue(
      practiceGymHealth === 'error' ? throwError(() => new Error('fail')) : of(practiceGymHealth),
    ),
    getMasteryValidationSummary: jasmine.createSpy('getMasteryValidationSummary').and.returnValue(
      mastery === 'error' ? throwError(() => new Error('fail')) : of(mastery),
    ),
  };
}

describe('AdminDeliveryHealthComponent', () => {
  let fixture: ComponentFixture<AdminDeliveryHealthComponent>;
  let component: AdminDeliveryHealthComponent;
  let api: ReturnType<typeof makeApi>;

  async function setup(
    todayHealth: AdminDeliveryHealth | 'error' = TODAY_HEALTH,
    practiceGymHealth: AdminDeliveryHealth | 'error' = PRACTICE_GYM_HEALTH,
    mastery: MasteryValidationSummary | 'error' = MASTERY_VALIDATION,
  ) {
    api = makeApi(todayHealth, practiceGymHealth, mastery);
    await TestBed.configureTestingModule({
      imports: [AdminDeliveryHealthComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminDeliveryHealthComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the delivery health page', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Delivery Health');
  });

  it('calls both delivery-health endpoints and the mastery endpoint on init', async () => {
    await setup();
    expect(api.getTodayPlanDeliveryHealth).toHaveBeenCalledTimes(1);
    expect(api.getPracticeGymDeliveryHealth).toHaveBeenCalledTimes(1);
    expect(api.getMasteryValidationSummary).toHaveBeenCalledTimes(1);
  });

  it('populates today health on success', async () => {
    await setup();
    expect(component.todayHealth()).toEqual(TODAY_HEALTH);
    expect(component.todayLoading()).toBeFalse();
  });

  it('shows error when Today delivery health fails', async () => {
    await setup('error');
    expect(component.todayError()).toBeTruthy();
    expect(component.todayLoading()).toBeFalse();
  });

  it('populates Practice Gym health on success', async () => {
    await setup();
    expect(component.practiceGymHealth()).toEqual(PRACTICE_GYM_HEALTH);
    expect(component.practiceGymLoading()).toBeFalse();
  });

  it('shows error when Practice Gym delivery health fails', async () => {
    await setup(TODAY_HEALTH, 'error');
    expect(component.practiceGymError()).toBeTruthy();
  });

  it('todaySelectedRatePct computes the selected/(selected+fallback) percentage', async () => {
    await setup();
    // 7 selected / (7 selected + 2 fallback) = 78%
    expect(component.todaySelectedRatePct()).toBe(78);
  });

  it('todayCefrBreakdown produces one entry per eligible CEFR level', async () => {
    await setup();
    expect(component.todayCefrBreakdown().length).toBe(2);
    expect(component.todayCefrBreakdown().map(b => b.label)).toEqual(['A1', 'B1']);
  });

  it('todayBankGaps flags CEFR levels with zero approved modules', async () => {
    await setup();
    expect(component.todayBankGaps().length).toBe(1);
    expect(component.todayBankGaps()[0].cefrLevel).toBe('A1');
    expect(fixture.nativeElement.textContent).toContain('Bank coverage gap');
  });

  it('shows top fallback reasons when present', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('No approved module for CEFR level A1');
  });

  it('does not show a bank coverage gap banner for Practice Gym when there is none', async () => {
    await setup();
    expect(component.practiceGymBankGaps().length).toBe(0);
  });

  it('calls getMasteryValidationSummary on init', async () => {
    await setup();
    expect(api.getMasteryValidationSummary).toHaveBeenCalledTimes(1);
  });

  it('populates masteryValidation on success', async () => {
    await setup();
    expect(component.masteryValidation()).not.toBeNull();
    expect(component.masteryLoading()).toBeFalse();
    expect(component.masteryError()).toBe('');
  });

  it('shows error when mastery validation API fails', async () => {
    await setup(TODAY_HEALTH, PRACTICE_GYM_HEALTH, 'error');
    expect(component.masteryError()).toBeTruthy();
    expect(component.masteryValidation()).toBeNull();
  });

  it('masteryBreakdownItems computes a distribution from mastery validation', async () => {
    await setup(TODAY_HEALTH, PRACTICE_GYM_HEALTH, {
      ...MASTERY_VALIDATION,
      countMastered: 10,
      countNeedsReview: 5,
      countAtRisk: 2,
    });
    const items = component.masteryBreakdownItems();
    expect(items.find(i => i.label === 'Mastered')?.value).toBe(10);
    expect(items.find(i => i.label === 'Needs review')?.value).toBe(5);
    expect(items.find(i => i.label === 'At risk')?.value).toBe(2);
  });

  it('refreshTodayHealth reloads Today delivery health', async () => {
    await setup();
    api.getTodayPlanDeliveryHealth.calls.reset();
    component.refreshTodayHealth();
    expect(api.getTodayPlanDeliveryHealth).toHaveBeenCalledTimes(1);
  });

  it('refreshMasteryValidation reloads the mastery summary', async () => {
    await setup();
    api.getMasteryValidationSummary.calls.reset();
    component.refreshMasteryValidation();
    expect(api.getMasteryValidationSummary).toHaveBeenCalledTimes(1);
  });
});
