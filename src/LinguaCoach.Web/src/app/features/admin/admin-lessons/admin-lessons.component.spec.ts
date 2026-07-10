import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminLessonsComponent } from './admin-lessons.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AdminGenerationBatchesResponse, AdminGenerationSettings, AdminGenerateLessonsResponse, MasteryValidationSummary } from '../../../core/models/admin.models';

// Phase I2C: the delivery-queue aggregate health, review scaffold dry-run/pending-review/
// approval, and Practice Gym review scaffold pilot sections (and the tests below that covered
// them) were removed along with the readiness pool. See
// docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.

const SETTINGS: AdminGenerationSettings = {
  readyLessonBufferSize: 5,
  refillThreshold: 2,
  refillBatchSize: 3,
  maxGenerationAttempts: 3,
  generationTimeoutSeconds: 120,
  ttsTimeoutSeconds: 60,
  maxConcurrentGenerationJobs: 2,
  maxConcurrentTtsJobs: 4,
  enableBackgroundGeneration: true,
  enableTtsGeneration: true,
  practiceGymReadyExercisesPerType: 3,
  practiceGymRefillThresholdPerType: 1,
  practiceGymRefillCountPerType: 2,
  updatedAtUtc: '2026-06-01T00:00:00Z',
};

const BATCHES: AdminGenerationBatchesResponse = {
  summary: { queued: 0, running: 0, failed: 0, lastSuccessfulGenerationUtc: null },
  readyBufferPerStudent: [{ studentProfileId: 'sp-1', readyCount: 3 }],
  batches: [],
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
  settings: AdminGenerationSettings | 'error' = SETTINGS,
  batches: AdminGenerationBatchesResponse | 'error' = BATCHES,
) {
  return {
    getGenerationSettings: jasmine.createSpy('getGenerationSettings').and.returnValue(
      settings === 'error' ? throwError(() => new Error('fail')) : of(settings),
    ),
    getGenerationBatches: jasmine.createSpy('getGenerationBatches').and.returnValue(
      batches === 'error' ? throwError(() => new Error('fail')) : of(batches),
    ),
    updateGenerationSettings: jasmine.createSpy('updateGenerationSettings').and.returnValue(of({ ...SETTINGS, updatedAtUtc: '2026-06-02T00:00:00Z' })),
    generateLessonsForStudent: jasmine.createSpy('generateLessonsForStudent').and.returnValue(of({ queued: true, requestedCount: 1 } as AdminGenerateLessonsResponse)),
    getMasteryValidationSummary: jasmine.createSpy('getMasteryValidationSummary').and.returnValue(of(MASTERY_VALIDATION)),
  };
}

describe('AdminLessonsComponent', () => {
  let fixture: ComponentFixture<AdminLessonsComponent>;
  let component: AdminLessonsComponent;
  let api: ReturnType<typeof makeApi>;

  async function setup(
    settings: AdminGenerationSettings | 'error' = SETTINGS,
    batches: AdminGenerationBatchesResponse | 'error' = BATCHES,
  ) {
    api = makeApi(settings, batches);
    await TestBed.configureTestingModule({
      imports: [AdminLessonsComponent],
      providers: [
        provideRouter([]),
        { provide: AdminApiService, useValue: api },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminLessonsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the lessons page', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Lessons');
  });

  it('calls getGenerationSettings and getGenerationBatches on init', async () => {
    await setup();
    expect(api.getGenerationSettings).toHaveBeenCalledTimes(1);
    expect(api.getGenerationBatches).toHaveBeenCalledTimes(1);
  });

  it('populates settings form fields on success', async () => {
    await setup();
    expect(component.readyLessonBufferSize).toBe(5);
    expect(component.refillThreshold).toBe(2);
    expect(component.settingsLoading()).toBeFalse();
  });

  it('shows error when settings API fails', async () => {
    await setup('error');
    expect(component.settingsError()).toBeTruthy();
    expect(component.settingsLoading()).toBeFalse();
  });

  it('populates batches on success', async () => {
    await setup();
    expect(component.batches()).toBeTruthy();
    expect(component.batchesLoading()).toBeFalse();
  });

  it('totalReady computed sums readyCount from buffer', async () => {
    await setup();
    expect(component.totalReady()).toBe(3);
  });

  it('studentsBuffered computed counts students with ready lessons', async () => {
    await setup();
    expect(component.studentsBuffered()).toBe(1);
  });

  it('shows error when batches API fails', async () => {
    await setup(SETTINGS, 'error');
    expect(component.batchesError()).toBeTruthy();
  });

  it('generateLessons shows error when studentProfileId is empty', async () => {
    await setup();
    component.studentProfileId = '';
    component.generateLessons();
    expect(component.generateError()).toBeTruthy();
    expect(api.generateLessonsForStudent).not.toHaveBeenCalled();
  });

  it('generateLessons calls API when studentProfileId is set', async () => {
    await setup();
    component.studentProfileId = 'sp-1';
    component.generateLessons();
    expect(api.generateLessonsForStudent).toHaveBeenCalledWith('sp-1');
  });

  it('saveSettings calls updateGenerationSettings', async () => {
    await setup();
    component.saveSettings();
    expect(api.updateGenerationSettings).toHaveBeenCalledTimes(1);
  });

  it('refreshBatches reloads batches', async () => {
    await setup();
    api.getGenerationBatches.calls.reset();
    component.refreshBatches();
    expect(api.getGenerationBatches).toHaveBeenCalledTimes(1);
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
    api = makeApi();
    api.getMasteryValidationSummary.and.returnValue(throwError(() => new Error('fail')));
    await TestBed.configureTestingModule({
      imports: [AdminLessonsComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminLessonsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.masteryError()).toBeTruthy();
    expect(component.masteryValidation()).toBeNull();
  });

  it('masteryBreakdownItems computes a distribution from mastery validation', async () => {
    api = makeApi();
    api.getMasteryValidationSummary.and.returnValue(of({
      ...MASTERY_VALIDATION,
      countMastered: 10,
      countNeedsReview: 5,
      countAtRisk: 2,
    }));
    await TestBed.configureTestingModule({
      imports: [AdminLessonsComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminLessonsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const items = component.masteryBreakdownItems();
    expect(items.find(i => i.label === 'Mastered')?.value).toBe(10);
    expect(items.find(i => i.label === 'Needs review')?.value).toBe(5);
    expect(items.find(i => i.label === 'At risk')?.value).toBe(2);
  });

  it('refreshMasteryValidation reloads mastery validation', async () => {
    await setup();
    api.getMasteryValidationSummary.calls.reset();
    component.refreshMasteryValidation();
    expect(api.getMasteryValidationSummary).toHaveBeenCalledTimes(1);
  });
});
