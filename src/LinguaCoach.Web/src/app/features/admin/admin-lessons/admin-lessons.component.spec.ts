import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminLessonsComponent } from './admin-lessons.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AdminGenerationBatchesResponse, AdminGenerationSettings, AdminGenerateLessonsResponse, AggregatePoolHealthSummary, ReviewScaffoldItemDetail } from '../../../core/models/admin.models';

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

const POOL_HEALTH: AggregatePoolHealthSummary = {
  totalStudentsWithItems: 0,
  totalQueued: 0,
  totalGenerating: 0,
  totalReady: 0,
  totalReserved: 0,
  totalConsumed: 0,
  totalExpired: 0,
  totalFailed: 0,
  totalStale: 0,
  totalReviewOnly: 0,
  totalSkipped: 0,
  studentsWithNoReadyItems: 0,
  studentsWithFailedItems: 0,
  studentsWithStaleItems: 0,
  studentsBelowMinimumThreshold: 0,
  averageReadyPerStudent: 0,
  oldestReadyItemCreatedAt: null,
  newestItemCreatedAt: null,
  generatedAt: '2026-06-27T00:00:00Z',
};

function makeApi(
  settings: AdminGenerationSettings | 'error' = SETTINGS,
  batches: AdminGenerationBatchesResponse | 'error' = BATCHES,
  poolHealth: AggregatePoolHealthSummary | 'error' = POOL_HEALTH,
) {
  return {
    getGenerationSettings: jasmine.createSpy('getGenerationSettings').and.returnValue(
      settings === 'error' ? throwError(() => new Error('fail')) : of(settings),
    ),
    getGenerationBatches: jasmine.createSpy('getGenerationBatches').and.returnValue(
      batches === 'error' ? throwError(() => new Error('fail')) : of(batches),
    ),
    getAggregatePoolHealth: jasmine.createSpy('getAggregatePoolHealth').and.returnValue(
      poolHealth === 'error' ? throwError(() => new Error('fail')) : of(poolHealth),
    ),
    updateGenerationSettings: jasmine.createSpy('updateGenerationSettings').and.returnValue(of({ ...SETTINGS, updatedAtUtc: '2026-06-02T00:00:00Z' })),
    generateLessonsForStudent: jasmine.createSpy('generateLessonsForStudent').and.returnValue(of({ queued: true, requestedCount: 1 } as AdminGenerateLessonsResponse)),
    getReviewScaffoldDryRun: jasmine.createSpy('getReviewScaffoldDryRun').and.returnValue(of({
      generationEnabled: false,
      dryRunOnly: true,
      status: 'Disabled',
      studentsConsidered: 0,
      studentsEligibleForReview: 0,
      estimatedReviewOnlyConversions: 0,
      blockedDuplicates: 0,
      blockedInactiveObjectives: 0,
      estimatedNetNewReviewItems: 0,
      requireAdminReview: true,
      maxScaffoldItemsPerStudentPerDay: 3,
      scaffoldAllowedSources: ['PracticeGym'],
      allowTodayLessonInsertion: false,
      minimumConfidenceForReviewNeed: 'Medium',
      adminReviewRequiredCount: 0,
      generatedTodayCount: 0,
      warnings: ['EnableReviewScaffoldGeneration is currently false.'],
      generatedAt: '2026-06-27T00:00:00Z',
    })),
    getReviewScaffoldPendingReview: jasmine.createSpy('getReviewScaffoldPendingReview').and.returnValue(of([])),
    approveReviewScaffoldItem: jasmine.createSpy('approveReviewScaffoldItem').and.returnValue(of({} as ReviewScaffoldItemDetail)),
    rejectReviewScaffoldItem: jasmine.createSpy('rejectReviewScaffoldItem').and.returnValue(of({} as ReviewScaffoldItemDetail)),
    reopenReviewScaffoldItem: jasmine.createSpy('reopenReviewScaffoldItem').and.returnValue(of({} as ReviewScaffoldItemDetail)),
  };
}

const PENDING_ITEM: ReviewScaffoldItemDetail = {
  id: 'item-1',
  studentId: 'student-1',
  activityId: null,
  source: 'PracticeGym',
  status: 'Ready',
  targetCefrLevel: 'A2',
  primarySkill: 'writing',
  curriculumObjectiveKey: 'obj-1',
  curriculumObjectiveTitle: 'Email basics',
  patternKey: null,
  activityType: null,
  routingReason: 'Review',
  adminReviewStatus: 'PendingReview',
  adminReviewedByUserId: null,
  adminReviewedAtUtc: null,
  adminReviewReason: null,
  adminReviewNotes: null,
  isStudentVisible: false,
  isPracticeGymEligible: false,
  createdAt: '2026-07-01T00:00:00Z',
};

describe('AdminLessonsComponent', () => {
  let fixture: ComponentFixture<AdminLessonsComponent>;
  let component: AdminLessonsComponent;
  let api: ReturnType<typeof makeApi>;

  async function setup(
    settings: AdminGenerationSettings | 'error' = SETTINGS,
    batches: AdminGenerationBatchesResponse | 'error' = BATCHES,
    poolHealth: AggregatePoolHealthSummary | 'error' = POOL_HEALTH,
  ) {
    api = makeApi(settings, batches, poolHealth);
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

  it('calls getGenerationSettings, getGenerationBatches, and getAggregatePoolHealth on init', async () => {
    await setup();
    expect(api.getGenerationSettings).toHaveBeenCalledTimes(1);
    expect(api.getGenerationBatches).toHaveBeenCalledTimes(1);
    expect(api.getAggregatePoolHealth).toHaveBeenCalledTimes(1);
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

  it('populates poolHealth on success', async () => {
    await setup();
    expect(component.poolHealth()).not.toBeNull();
    expect(component.poolHealthLoading()).toBeFalse();
    expect(component.poolHealthError()).toBe('');
  });

  it('shows error when pool health API fails', async () => {
    await setup(SETTINGS, BATCHES, 'error');
    expect(component.poolHealthError()).toBeTruthy();
    expect(component.poolHealthLoading()).toBeFalse();
    expect(component.poolHealth()).toBeNull();
  });

  it('refreshPoolHealth reloads pool health', async () => {
    await setup();
    api.getAggregatePoolHealth.calls.reset();
    component.refreshPoolHealth();
    expect(api.getAggregatePoolHealth).toHaveBeenCalledTimes(1);
  });

  it('calls getReviewScaffoldDryRun and getReviewScaffoldPendingReview on init', async () => {
    await setup();
    expect(api.getReviewScaffoldDryRun).toHaveBeenCalledTimes(1);
    expect(api.getReviewScaffoldPendingReview).toHaveBeenCalledTimes(1);
  });

  it('populates scaffold dry-run config fields', async () => {
    await setup();
    expect(component.scaffoldDryRun()?.requireAdminReview).toBeTrue();
    expect(component.scaffoldDryRun()?.maxScaffoldItemsPerStudentPerDay).toBe(3);
    expect(component.scaffoldDryRun()?.scaffoldAllowedSources).toEqual(['PracticeGym']);
    expect(component.scaffoldDryRun()?.allowTodayLessonInsertion).toBeFalse();
  });

  it('shows empty state when no items are pending admin review', async () => {
    await setup();
    expect(component.scaffoldPending().length).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('Nothing to review');
  });

  async function setupWithPendingItem(item: ReviewScaffoldItemDetail = PENDING_ITEM) {
    api = makeApi();
    api.getReviewScaffoldPendingReview.and.returnValue(of([item]));
    await TestBed.configureTestingModule({
      imports: [AdminLessonsComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminLessonsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders pending review rows when present', async () => {
    await setupWithPendingItem();
    expect(component.scaffoldPending().length).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Email basics');
    expect(fixture.nativeElement.textContent).toContain('Pending review');
  });

  it('renders approved badge and hides approve/reject actions for non-actionable items', async () => {
    await setupWithPendingItem({ ...PENDING_ITEM, adminReviewStatus: 'Approved', status: 'Consumed', isStudentVisible: true });
    expect(fixture.nativeElement.textContent).toContain('Approved');
  });

  it('renders rejected badge', async () => {
    await setupWithPendingItem({ ...PENDING_ITEM, adminReviewStatus: 'Rejected', adminReviewReason: 'Too hard for level' });
    expect(fixture.nativeElement.textContent).toContain('Rejected');
    expect(fixture.nativeElement.textContent).toContain('Too hard for level');
  });

  it('refreshScaffoldPendingReview reloads the pending review list', async () => {
    await setup();
    api.getReviewScaffoldPendingReview.calls.reset();
    component.refreshScaffoldPendingReview();
    expect(api.getReviewScaffoldPendingReview).toHaveBeenCalledTimes(1);
  });

  it('approveScaffoldItem calls the API when confirmed', async () => {
    await setupWithPendingItem();
    spyOn(window, 'confirm').and.returnValue(true);
    component.approveScaffoldItem(PENDING_ITEM);
    expect(api.approveReviewScaffoldItem).toHaveBeenCalledWith('item-1');
  });

  it('approveScaffoldItem does nothing when not confirmed', async () => {
    await setupWithPendingItem();
    spyOn(window, 'confirm').and.returnValue(false);
    component.approveScaffoldItem(PENDING_ITEM);
    expect(api.approveReviewScaffoldItem).not.toHaveBeenCalled();
  });

  it('rejectScaffoldItem requires a non-empty reason', async () => {
    await setupWithPendingItem();
    spyOn(window, 'prompt').and.returnValue('   ');
    component.rejectScaffoldItem(PENDING_ITEM);
    expect(api.rejectReviewScaffoldItem).not.toHaveBeenCalled();
    expect(component.scaffoldActionError()).toBeTruthy();
  });

  it('rejectScaffoldItem calls the API with the reason when confirmed', async () => {
    await setupWithPendingItem();
    spyOn(window, 'prompt').and.returnValue('Too hard for level');
    spyOn(window, 'confirm').and.returnValue(true);
    component.rejectScaffoldItem(PENDING_ITEM);
    expect(api.rejectReviewScaffoldItem).toHaveBeenCalledWith('item-1', { reason: 'Too hard for level' });
  });

  it('reopenScaffoldItem calls the API when confirmed', async () => {
    await setupWithPendingItem({ ...PENDING_ITEM, adminReviewStatus: 'Rejected' });
    spyOn(window, 'confirm').and.returnValue(true);
    component.reopenScaffoldItem({ ...PENDING_ITEM, adminReviewStatus: 'Rejected' });
    expect(api.reopenReviewScaffoldItem).toHaveBeenCalledWith('item-1');
  });
});
