import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminErrorStateComponent,
  SpAdminFormFieldComponent,
  SpAdminFormGridComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminNumberInputComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminTableComponent,
  SpAdminToggleComponent,
} from '../../../design-system/admin';
import { SpAdminNotImplementedStateComponent } from '../../../design-system/admin/components/not-implemented-state/sp-admin-not-implemented-state.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AdminGenerationBatchesResponse, AggregatePoolHealthSummary, ReviewScaffoldDryRunSummary } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-lessons',
  standalone: true,
  templateUrl: './admin-lessons.component.html',
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminErrorStateComponent,
    SpAdminFormFieldComponent,
    SpAdminFormGridComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminNotImplementedStateComponent,
    SpAdminNumberInputComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminTableComponent,
    SpAdminToggleComponent,
  ],
})
export class AdminLessonsComponent implements OnInit {
  constructor(private adminApi: AdminApiService) {}

  // ── Settings load state ───────────────────────────────────────────────────
  settingsLoading = signal(false);
  settingsError = signal('');
  settingsUpdatedAt = signal<string | null>(null);

  // Form fields — plain properties for ngModel / CVA binding
  readyLessonBufferSize: number | null = 5;
  refillThreshold: number | null = 2;
  refillBatchSize: number | null = 3;
  maxGenerationAttempts: number | null = 3;
  generationTimeoutSeconds: number | null = 120;
  ttsTimeoutSeconds: number | null = 60;
  maxConcurrentGenerationJobs: number | null = 2;
  maxConcurrentTtsJobs: number | null = 4;
  practiceGymReadyExercisesPerType: number | null = 3;
  practiceGymRefillThresholdPerType: number | null = 1;
  practiceGymRefillCountPerType: number | null = 2;
  bgGenEnabled = signal(true);
  ttsEnabled = signal(true);

  saveStatus = signal('');
  savePending = signal(false);

  // ── Batches / buffer table ────────────────────────────────────────────────
  batchesLoading = signal(false);
  batchesError = signal('');
  batches = signal<AdminGenerationBatchesResponse | null>(null);

  totalReady = computed(() =>
    this.batches()?.readyBufferPerStudent.reduce((s, e) => s + e.readyCount, 0) ?? null
  );
  studentsBuffered = computed(() =>
    this.batches()?.readyBufferPerStudent.filter(e => e.readyCount > 0).length ?? null
  );

  // ── Aggregate pool health ─────────────────────────────────────────────────
  poolHealthLoading = signal(false);
  poolHealthError = signal('');
  poolHealth = signal<AggregatePoolHealthSummary | null>(null);

  // ── Review scaffold dry-run ───────────────────────────────────────────────
  scaffoldDryRunLoading = signal(false);
  scaffoldDryRunError = signal('');
  scaffoldDryRun = signal<ReviewScaffoldDryRunSummary | null>(null);

  // ── Generate for student ──────────────────────────────────────────────────
  studentProfileId = '';
  generatePending = signal(false);
  generateStatus = signal('');
  generateError = signal('');

  ngOnInit(): void {
    this.loadSettings();
    this.loadBatches();
    this.loadPoolHealth();
    this.loadScaffoldDryRun();
  }

  private loadSettings(): void {
    this.settingsLoading.set(true);
    this.settingsError.set('');
    this.adminApi.getGenerationSettings().subscribe({
      next: s => {
        this.readyLessonBufferSize = s.readyLessonBufferSize;
        this.refillThreshold = s.refillThreshold;
        this.refillBatchSize = s.refillBatchSize;
        this.maxGenerationAttempts = s.maxGenerationAttempts;
        this.generationTimeoutSeconds = s.generationTimeoutSeconds;
        this.ttsTimeoutSeconds = s.ttsTimeoutSeconds;
        this.maxConcurrentGenerationJobs = s.maxConcurrentGenerationJobs;
        this.maxConcurrentTtsJobs = s.maxConcurrentTtsJobs;
        this.practiceGymReadyExercisesPerType = s.practiceGymReadyExercisesPerType;
        this.practiceGymRefillThresholdPerType = s.practiceGymRefillThresholdPerType;
        this.practiceGymRefillCountPerType = s.practiceGymRefillCountPerType;
        this.bgGenEnabled.set(s.enableBackgroundGeneration);
        this.ttsEnabled.set(s.enableTtsGeneration);
        this.settingsUpdatedAt.set(s.updatedAtUtc);
        this.settingsLoading.set(false);
      },
      error: err => {
        this.settingsError.set(err?.error?.error ?? err?.message ?? 'Failed to load generation settings.');
        this.settingsLoading.set(false);
      },
    });
  }

  private loadBatches(): void {
    this.batchesLoading.set(true);
    this.batchesError.set('');
    this.adminApi.getGenerationBatches().subscribe({
      next: b => { this.batches.set(b); this.batchesLoading.set(false); },
      error: err => {
        this.batchesError.set(err?.error?.error ?? err?.message ?? 'Failed to load generation batches.');
        this.batchesLoading.set(false);
      },
    });
  }

  generateLessons(): void {
    const id = this.studentProfileId.trim();
    if (!id) { this.generateError.set('Enter a student profile ID.'); return; }
    this.generatePending.set(true);
    this.generateStatus.set('');
    this.generateError.set('');
    this.adminApi.generateLessonsForStudent(id).subscribe({
      next: res => {
        this.generateStatus.set(`Queued — ${res.requestedCount} lesson(s) requested.`);
        this.generatePending.set(false);
        this.loadBatches();
      },
      error: err => {
        this.generateError.set(err?.error?.error ?? err?.message ?? 'Generation request failed.');
        this.generatePending.set(false);
      },
    });
  }

  saveSettings(): void {
    this.savePending.set(true);
    this.saveStatus.set('');
    this.adminApi.updateGenerationSettings({
      readyLessonBufferSize: this.readyLessonBufferSize ?? 5,
      refillThreshold: this.refillThreshold ?? 2,
      refillBatchSize: this.refillBatchSize ?? 3,
      maxGenerationAttempts: this.maxGenerationAttempts ?? 3,
      generationTimeoutSeconds: this.generationTimeoutSeconds ?? 120,
      ttsTimeoutSeconds: this.ttsTimeoutSeconds ?? 60,
      maxConcurrentGenerationJobs: this.maxConcurrentGenerationJobs ?? 2,
      maxConcurrentTtsJobs: this.maxConcurrentTtsJobs ?? 4,
      enableBackgroundGeneration: this.bgGenEnabled(),
      enableTtsGeneration: this.ttsEnabled(),
      practiceGymReadyExercisesPerType: this.practiceGymReadyExercisesPerType ?? 3,
      practiceGymRefillThresholdPerType: this.practiceGymRefillThresholdPerType ?? 1,
      practiceGymRefillCountPerType: this.practiceGymRefillCountPerType ?? 2,
    }).subscribe({
      next: s => {
        this.settingsUpdatedAt.set(s.updatedAtUtc);
        this.saveStatus.set('Settings saved.');
        this.savePending.set(false);
      },
      error: err => {
        this.saveStatus.set(err?.error?.error ?? err?.message ?? 'Save failed.');
        this.savePending.set(false);
      },
    });
  }

  refreshBatches(): void { this.loadBatches(); }

  private loadPoolHealth(): void {
    this.poolHealthLoading.set(true);
    this.poolHealthError.set('');
    this.adminApi.getAggregatePoolHealth().subscribe({
      next: h => { this.poolHealth.set(h); this.poolHealthLoading.set(false); },
      error: err => {
        this.poolHealthError.set(err?.error?.error ?? err?.message ?? 'Failed to load aggregate pool health.');
        this.poolHealthLoading.set(false);
      },
    });
  }

  refreshPoolHealth(): void { this.loadPoolHealth(); }

  private loadScaffoldDryRun(): void {
    this.scaffoldDryRunLoading.set(true);
    this.scaffoldDryRunError.set('');
    this.adminApi.getReviewScaffoldDryRun().subscribe({
      next: s => { this.scaffoldDryRun.set(s); this.scaffoldDryRunLoading.set(false); },
      error: err => {
        this.scaffoldDryRunError.set(err?.error?.error ?? err?.message ?? 'Failed to load review scaffold status.');
        this.scaffoldDryRunLoading.set(false);
      },
    });
  }

  refreshScaffoldDryRun(): void { this.loadScaffoldDryRun(); }
}
