import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminIntegrationsComponent } from './admin-integrations.component';
import {
  AdminIntegrationsService,
  StorageSettings,
  StorageTestResult,
  GenerationSettings,
  BatchesResponse,
} from '../../../core/services/admin-integrations.service';

const STORAGE: StorageSettings = {
  provider: 'minio',
  endpoint: 'http://minio:9000',
  bucketName: 'speakpath',
  accessKey: 'configured',
  secretKey: 'configured',
  useSsl: false,
  signedUrlExpiryMinutes: 60,
};

const SETTINGS: GenerationSettings = {
  readyLessonBufferSize: 3,
  refillThreshold: 1,
  refillBatchSize: 2,
  maxGenerationAttempts: 3,
  generationTimeoutSeconds: 120,
  ttsTimeoutSeconds: 60,
  maxConcurrentGenerationJobs: 2,
  maxConcurrentTtsJobs: 2,
  enableBackgroundGeneration: true,
  enableTtsGeneration: true,
  practiceGymReadyExercisesPerType: 5,
  practiceGymRefillThresholdPerType: 2,
  practiceGymRefillCountPerType: 3,
};

const BATCHES: BatchesResponse = {
  summary: { queued: 1, running: 0, failed: 2, lastSuccessfulGenerationUtc: '2026-06-19T08:00:00Z' },
  readyBufferPerStudent: [
    { studentProfileId: 'sp-abc-123', readyCount: 2 },
  ],
  batches: [
    {
      id: 'batch-1',
      studentProfileId: 'sp-abc-123',
      triggerReason: 'ManualTrigger',
      status: 'Completed',
      requestedSessionCount: 3,
      completedSessionCount: 3,
      providerName: 'anthropic',
      modelName: 'claude-sonnet-4-6',
      startedAtUtc: '2026-06-19T07:55:00Z',
      completedAtUtc: '2026-06-19T08:00:00Z',
      failureReason: null,
      createdAt: '2026-06-19T07:50:00Z',
    },
    {
      id: 'batch-2',
      studentProfileId: 'sp-def-456',
      triggerReason: 'BackgroundRefill',
      status: 'Failed',
      requestedSessionCount: 2,
      completedSessionCount: 0,
      providerName: null,
      modelName: null,
      startedAtUtc: null,
      completedAtUtc: null,
      failureReason: 'AI provider returned 429',
      createdAt: '2026-06-19T07:40:00Z',
    },
  ],
};

const STORAGE_TEST_OK: StorageTestResult = { ok: true, lastCheckedUtc: '2026-06-19T10:00:00Z', error: null };
const STORAGE_TEST_FAIL: StorageTestResult = { ok: false, lastCheckedUtc: '2026-06-19T10:01:00Z', error: 'Connection refused' };

function makeSvc() {
  return {
    getStorage: jasmine.createSpy('getStorage').and.returnValue(of(STORAGE)),
    getGenerationSettings: jasmine.createSpy('getGenerationSettings').and.returnValue(of(SETTINGS)),
    getBatches: jasmine.createSpy('getBatches').and.returnValue(of(BATCHES)),
    testStorage: jasmine.createSpy('testStorage').and.returnValue(of(STORAGE_TEST_OK)),
    updateGenerationSettings: jasmine.createSpy('updateGenerationSettings').and.returnValue(of(SETTINGS)),
    retryBatch: jasmine.createSpy('retryBatch').and.returnValue(of(undefined)),
    cancelBatch: jasmine.createSpy('cancelBatch').and.returnValue(of(undefined)),
    generateLessons: jasmine.createSpy('generateLessons').and.returnValue(of({ requestedCount: 3 })),
  };
}

describe('AdminIntegrationsComponent', () => {
  let fixture: ComponentFixture<AdminIntegrationsComponent>;
  let component: AdminIntegrationsComponent;
  let svc: ReturnType<typeof makeSvc>;

  async function setup() {
    svc = makeSvc();
    await TestBed.configureTestingModule({
      imports: [AdminIntegrationsComponent],
      providers: [{ provide: AdminIntegrationsService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminIntegrationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the page', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Integrations');
  });

  it('calls getStorage, getGenerationSettings, getBatches on init', async () => {
    await setup();
    expect(svc.getStorage).toHaveBeenCalledTimes(1);
    expect(svc.getGenerationSettings).toHaveBeenCalledTimes(1);
    expect(svc.getBatches).toHaveBeenCalledTimes(1);
  });

  it('renders storage section', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('MinIO / Object Storage');
    expect(fixture.nativeElement.textContent).toContain('Provider');
    expect(fixture.nativeElement.textContent).toContain('Signed URL expiry');
  });

  it('renders configured badges for storage keys', async () => {
    await setup();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Configured');
  });

  it('renders not-set badges when access/secret key absent', async () => {
    svc = makeSvc();
    svc.getStorage.and.returnValue(of({ ...STORAGE, accessKey: null, secretKey: null }));
    await TestBed.configureTestingModule({
      imports: [AdminIntegrationsComponent],
      providers: [{ provide: AdminIntegrationsService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminIntegrationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Not set');
  });

  it('calls testStorage on testConnection', fakeAsync(async () => {
    await setup();
    component.testConnection();
    tick();
    expect(svc.testStorage).toHaveBeenCalledTimes(1);
    expect(component.storageTest()?.ok).toBeTrue();
  }));

  it('sets storageTest to failed result on test error', fakeAsync(async () => {
    await setup();
    svc.testStorage.and.returnValue(throwError(() => ({ error: { error: 'Connection refused' } })));
    component.testConnection();
    tick();
    expect(component.storageTest()?.ok).toBeFalse();
  }));

  it('renders generation settings fields', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Ready lesson buffer size');
    expect(fixture.nativeElement.textContent).toContain('TTS timeout');
  });

  it('calls updateGenerationSettings on saveSettings', fakeAsync(async () => {
    await setup();
    component.saveSettings();
    tick();
    expect(svc.updateGenerationSettings).toHaveBeenCalledTimes(1);
    expect(component.settingsSaved()).toBeTrue();
  }));

  it('does not call updateGenerationSettings when settings is null', async () => {
    await setup();
    component.settings.set(null);
    component.saveSettings();
    expect(svc.updateGenerationSettings).not.toHaveBeenCalled();
  });

  it('sets settingsError on save failure', fakeAsync(async () => {
    await setup();
    svc.updateGenerationSettings.and.returnValue(throwError(() => ({ error: { error: 'Save failed' } })));
    component.saveSettings();
    tick();
    expect(component.settingsError()).toBe('Save failed');
  }));

  it('renders batch summary stat cards', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Queued');
    expect(fixture.nativeElement.textContent).toContain('Running');
    expect(fixture.nativeElement.textContent).toContain('Failed');
  });

  it('renders ready buffer per student rows', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('sp-abc-123');
  });

  it('renders batch rows with status badges', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Completed');
    expect(fixture.nativeElement.textContent).toContain('Failed');
  });

  it('renders failure reason in batch row', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('429');
  });

  it('calls retryBatch and reloads on retry', fakeAsync(async () => {
    await setup();
    component.retry('batch-2');
    tick();
    expect(svc.retryBatch).toHaveBeenCalledWith('batch-2');
    expect(svc.getBatches).toHaveBeenCalledTimes(2);
  }));

  it('calls cancelBatch and reloads on cancel', fakeAsync(async () => {
    await setup();
    component.cancel('batch-1');
    tick();
    expect(svc.cancelBatch).toHaveBeenCalledWith('batch-1');
    expect(svc.getBatches).toHaveBeenCalledTimes(2);
  }));

  it('sets batchesError on cancel failure', fakeAsync(async () => {
    await setup();
    svc.cancelBatch.and.returnValue(throwError(() => ({ error: { error: 'Cannot cancel' } })));
    component.cancel('batch-1');
    tick();
    expect(component.batchesError()).toBe('Cannot cancel');
  }));

  it('calls generateLessons and sets generateStatus', fakeAsync(async () => {
    await setup();
    component.generateStudentId = 'sp-abc-123';
    component.generateLessons();
    tick();
    expect(svc.generateLessons).toHaveBeenCalledWith('sp-abc-123');
    expect(component.generateStatus()).toContain('sp-abc-123');
  }));

  it('does not call generateLessons when studentId is empty', async () => {
    await setup();
    component.generateStudentId = '';
    component.generateLessons();
    expect(svc.generateLessons).not.toHaveBeenCalled();
  });

  it('batchStatusTone returns correct tones', async () => {
    await setup();
    expect(component.batchStatusTone('Completed')).toBe('success');
    expect(component.batchStatusTone('Failed')).toBe('danger');
    expect(component.batchStatusTone('Partial')).toBe('warning');
    expect(component.batchStatusTone('Running')).toBe('info');
    expect(component.batchStatusTone('Queued')).toBe('neutral');
    expect(component.batchStatusTone('Unknown')).toBe('neutral');
  });

  it('shows error state when storage fails to load', async () => {
    svc = makeSvc();
    svc.getStorage.and.returnValue(throwError(() => ({ error: { error: 'Storage down' } })));
    await TestBed.configureTestingModule({
      imports: [AdminIntegrationsComponent],
      providers: [{ provide: AdminIntegrationsService, useValue: svc }],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminIntegrationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.storageError()).toContain('Storage down');
  });

  it('generation settings signal holds numeric values from API', async () => {
    await setup();
    const s = component.settings();
    expect(s).toBeTruthy();
    expect(typeof s!.readyLessonBufferSize).toBe('number');
    expect(typeof s!.generationTimeoutSeconds).toBe('number');
    expect(typeof s!.maxConcurrentGenerationJobs).toBe('number');
  });

  it('generation settings signal holds boolean values for checkbox fields', async () => {
    await setup();
    const s = component.settings();
    expect(typeof s!.enableBackgroundGeneration).toBe('boolean');
    expect(typeof s!.enableTtsGeneration).toBe('boolean');
    expect(s!.enableBackgroundGeneration).toBeTrue();
    expect(s!.enableTtsGeneration).toBeTrue();
  });

  it('passes numeric values to updateGenerationSettings payload on save', fakeAsync(async () => {
    await setup();
    component.saveSettings();
    tick();
    const payload: GenerationSettings = svc.updateGenerationSettings.calls.mostRecent().args[0];
    expect(typeof payload.readyLessonBufferSize).toBe('number');
    expect(typeof payload.ttsTimeoutSeconds).toBe('number');
    expect(typeof payload.enableBackgroundGeneration).toBe('boolean');
  }));

  it('renders section headers for buffer and batches sub-sections', async () => {
    await setup();
    const headers = (fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-section-header');
    expect(headers.length).toBeGreaterThanOrEqual(2);
    expect(fixture.nativeElement.textContent).toContain('Ready lesson buffer per student');
    expect(fixture.nativeElement.textContent).toContain('Recent batches');
  });

  it('renders form-grid wrappers for settings and storage fields', async () => {
    await setup();
    const grids = (fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-form-grid');
    expect(grids.length).toBeGreaterThanOrEqual(2);
  });

  // ── Readiness pool aggregate placeholder card ─────────────────────────────

  it('renders readiness pool aggregate card title', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Readiness pool');
  });

  it('renders "Backend not available yet" in readiness pool card', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Backend not available yet');
  });

  it('existing batches and storage tests still pass alongside pool card', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Ready lesson buffer per student');
    expect(fixture.nativeElement.textContent).toContain('Recent batches');
  });
});
