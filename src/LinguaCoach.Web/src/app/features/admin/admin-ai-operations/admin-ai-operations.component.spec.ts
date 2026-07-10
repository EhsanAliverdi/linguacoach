import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminAiOperationsComponent } from './admin-ai-operations.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AdminAiOperationsSummary } from '../../../core/models/admin.models';

const SUMMARY: AdminAiOperationsSummary = {
  generatedAtUtc: '2026-07-02T00:00:00Z',
  overallStatus: 'Healthy',
  warnings: [],
  unavailableSections: [
    'RealTimeJobQueueDepth — no dedicated job/queue table exists; pending counts above are the closest available signal.',
  ],
  providerUsage: {
    totalCalls: 10,
    successfulCalls: 9,
    failedCalls: 1,
    fallbackCalls: 0,
    totalCostUsd: 1.23,
    totalInputTokens: 1000,
    totalOutputTokens: 500,
    totalTokens: 1500,
    zeroCostCallCount: 0,
    byProvider: [{ provider: 'OpenAI', calls: 10, successful: 9, fallback: 0, costUsd: 1.23 }],
    byFeature: [],
  },
  speakingEvaluationSummary: {
    configEnabled: true,
    providerName: 'FakeProvider',
    pendingCount: 1,
    completedCount: 5,
    failedCount: 1,
    notSupportedCount: 0,
    oldestPendingAgeMinutes: 12,
    providerModelDistribution: [],
    latestFailureReasons: ['Provider timeout while evaluating audio.'],
  },
  writingEvaluationSummary: {
    configEnabled: false,
    providerName: null,
    modelName: null,
    pendingCount: 0,
    evaluatingCount: 0,
    completedCount: 0,
    failedCount: 0,
    notSupportedCount: 0,
    oldestPendingAgeMinutes: null,
    latestFailureReasons: [],
  },
  generationQualitySummary: {
    totalValidationFailures: 2,
    abandonedGenerations: 0,
    recentFailureCount: 2,
    retentionDays: 90,
    patternBreakdown: [],
    cefrBreakdown: [],
    providerBreakdown: [],
    latestFailures: [],
  },
  signalGateSummary: {
    speakingCefrUpdatesEnabled: false,
    writingCefrUpdatesEnabled: false,
    speakingObjectiveCompletionEnabled: false,
    writingObjectiveCompletionEnabled: false,
    speakingLearningPlanAutoRegenEnabled: false,
    writingLearningPlanAutoRegenEnabled: false,
    speakingPositiveSignalsEnabled: false,
    writingPositiveSignalsEnabled: false,
    speakingReviewSignalsEnabled: true,
    writingReviewSignalsEnabled: true,
    anyInvariantViolationsDetected: false,
  },
  recentFailures: [
    {
      timestampUtc: '2026-07-02T00:00:00Z',
      area: 'Speaking',
      studentProfileId: 'student-1',
      evaluationId: 'eval-1',
      providerName: 'FakeProvider',
      modelName: 'fake-model',
      reason: 'Provider timeout while evaluating audio.',
      status: 'Failed',
    },
  ],
};

const EMPTY_SUMMARY: AdminAiOperationsSummary = {
  ...SUMMARY,
  providerUsage: { ...SUMMARY.providerUsage, totalCalls: 0 },
  speakingEvaluationSummary: { ...SUMMARY.speakingEvaluationSummary, pendingCount: 0, completedCount: 0, failedCount: 0 },
  writingEvaluationSummary: { ...SUMMARY.writingEvaluationSummary },
  generationQualitySummary: { ...SUMMARY.generationQualitySummary, totalValidationFailures: 0 },
  recentFailures: [],
};

function makeApi(summary: AdminAiOperationsSummary | 'error' = SUMMARY) {
  return {
    getAiOperationsSummary: jasmine.createSpy('getAiOperationsSummary').and.returnValue(
      summary === 'error' ? throwError(() => new Error('fail')) : of(summary),
    ),
  };
}

describe('AdminAiOperationsComponent', () => {
  let fixture: ComponentFixture<AdminAiOperationsComponent>;
  let component: AdminAiOperationsComponent;
  let api: ReturnType<typeof makeApi>;

  async function setup(summary: AdminAiOperationsSummary | 'error' = SUMMARY) {
    api = makeApi(summary);
    await TestBed.configureTestingModule({
      imports: [AdminAiOperationsComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminAiOperationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('loads the summary on init', async () => {
    await setup();
    expect(api.getAiOperationsSummary).toHaveBeenCalledTimes(1);
    expect(component.summary()).toEqual(SUMMARY);
  });

  it('shows a loading state before the summary resolves', () => {
    api = makeApi();
    TestBed.configureTestingModule({
      imports: [AdminAiOperationsComponent],
      providers: [provideRouter([]), { provide: AdminApiService, useValue: api }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminAiOperationsComponent);
    // No detectChanges — ngOnInit not yet resolved by the fake async observable, so still loading.
    expect(fixture.componentInstance.loading()).toBeTrue();
  });

  it('shows an error state when the summary request fails', async () => {
    await setup('error');
    const el = fixture.nativeElement.querySelector('[data-testid="ai-ops-error"]');
    expect(el).toBeTruthy();
  });

  it('shows the empty state when there is no AI activity', async () => {
    await setup(EMPTY_SUMMARY);
    const el = fixture.nativeElement.querySelector('[data-testid="ai-ops-empty"]');
    expect(el).toBeTruthy();
  });

  it('renders the overall status badge', async () => {
    await setup();
    const badge = fixture.nativeElement.querySelector('[data-testid="ai-ops-status-badge"]');
    expect(badge.textContent).toContain('Healthy');
  });

  it('renders provider usage KPI/status cards', async () => {
    await setup();
    const card = fixture.nativeElement.querySelector('[data-testid="ai-ops-provider-usage"]');
    expect(card.textContent).toContain('OpenAI');
  });

  it('renders speaking and writing evaluation operational counts', async () => {
    await setup();
    const speaking = fixture.nativeElement.querySelector('[data-testid="ai-ops-speaking"]');
    expect(speaking.textContent).toContain('FakeProvider');
    const speakingFailures = fixture.nativeElement.querySelector('[data-testid="ai-ops-speaking-failures"]');
    expect(speakingFailures.textContent).toContain('Provider timeout while evaluating audio.');
  });

  it('renders the safety gate card with disabled/enabled states', async () => {
    await setup();
    const gates = fixture.nativeElement.querySelector('[data-testid="ai-ops-safety-gates"]');
    expect(gates.textContent).toContain('Speaking AI can update CEFR');
    expect(gates.textContent).toContain('Disabled');
    // No invariant violation banner when everything is safe.
    expect(fixture.nativeElement.querySelector('[data-testid="ai-ops-invariant-violation"]')).toBeNull();
  });

  it('shows the invariant violation banner when a gate reports a violation', async () => {
    await setup({
      ...SUMMARY,
      signalGateSummary: { ...SUMMARY.signalGateSummary, anyInvariantViolationsDetected: true },
    });
    const banner = fixture.nativeElement.querySelector('[data-testid="ai-ops-invariant-violation"]');
    expect(banner).toBeTruthy();
  });

  it('renders the recent failures table with safe fields only', async () => {
    await setup();
    const table = fixture.nativeElement.querySelector('[data-testid="ai-ops-recent-failures"]');
    expect(table.textContent).toContain('Speaking');
    expect(table.textContent).toContain('Provider timeout while evaluating audio.');
  });

  it('renders "Not implemented yet" for unavailable metrics', async () => {
    await setup();
    const unavailable = fixture.nativeElement.querySelector('[data-testid="ai-ops-unavailable"]');
    expect(unavailable.textContent).toContain('Not implemented yet');
    expect(unavailable.textContent).toContain('RealTimeJobQueueDepth');
  });

  it('never renders raw prompt, provider payload, or secret text', async () => {
    await setup();
    const text = fixture.nativeElement.textContent.toLowerCase();
    expect(text).not.toContain('apikey');
    expect(text).not.toContain('api_key');
    expect(text).not.toContain('bearer ');
    expect(text).not.toContain('secret');
    expect(text).not.toContain('system prompt');
  });

  // Phase I2C: "renders review scaffold / pilot AI generation state" removed — the card it
  // checked (data-testid="ai-ops-readiness-pool") was deleted along with readinessPoolAiSummary.
  // See docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.

  it('refresh button reloads the summary', async () => {
    await setup();
    api.getAiOperationsSummary.calls.reset();
    component.load();
    expect(api.getAiOperationsSummary).toHaveBeenCalledTimes(1);
  });

  it('links to feature gate settings for locked signal safety gates', async () => {
    await setup();
    const speakingLink = fixture.nativeElement.querySelector('[data-testid="ai-ops-open-settings-speaking"]');
    const writingLink = fixture.nativeElement.querySelector('[data-testid="ai-ops-open-settings-writing"]');
    expect(speakingLink).toBeTruthy();
    expect(writingLink).toBeTruthy();
  });
});
