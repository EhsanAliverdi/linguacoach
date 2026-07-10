import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
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
import { SpAdminRingMetricComponent } from '../../../design-system/admin/components/ring-metric/sp-admin-ring-metric.component';
import { SpAdminBreakdownBarsComponent, BreakdownBarItem } from '../../../design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component';
import { SpAdminGraphCardComponent } from '../../../design-system/admin/components/graph-card/sp-admin-graph-card.component';
import { SpAdminVisualPlaceholderComponent } from '../../../design-system/admin/components/visual-placeholder/sp-admin-visual-placeholder.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AdminGenerationBatchesResponse, MasteryValidationSummary } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-today-delivery-health',
  standalone: true,
  templateUrl: './admin-today-delivery-health.component.html',
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
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
    SpAdminRingMetricComponent,
    SpAdminBreakdownBarsComponent,
    SpAdminGraphCardComponent,
    SpAdminVisualPlaceholderComponent,
  ],
})
export class AdminTodayDeliveryHealthComponent implements OnInit {
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

  // Phase I2C: aggregate pool health, review scaffold dry-run, review scaffold pending admin
  // review, and the Phase 19C pilot monitoring sections were removed here — the readiness pool
  // and AdminReadinessPoolController they read from were deleted. See
  // docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md.

  // ── Mastery validation (system-wide diagnostic) ───────────────────────────
  masteryLoading = signal(false);
  masteryError = signal('');
  masteryValidation = signal<MasteryValidationSummary | null>(null);

  // ── Generate for student ──────────────────────────────────────────────────
  studentProfileId = '';
  generatePending = signal(false);
  generateStatus = signal('');
  generateError = signal('');

  ngOnInit(): void {
    this.loadSettings();
    this.loadBatches();
    this.loadMasteryValidation();
  }

  // ── Mastery validation — chart data ─────────────────────────────────────────

  masteryBreakdownItems = computed<BreakdownBarItem[]>(() => {
    const m = this.masteryValidation();
    if (!m) return [];
    const values: [string, number, BreakdownBarItem['tone']][] = [
      ['Mastered', m.countMastered, 'green'],
      ['Needs review', m.countNeedsReview, 'amber'],
      ['At risk', m.countAtRisk, 'danger'],
      ['Insufficient evidence', m.countInsufficientEvidence, 'slate'],
    ];
    const max = Math.max(...values.map(v => v[1]), 1);
    return values
      .filter(([, value]) => value > 0)
      .map(([label, value, tone]) => ({ label, value, pct: Math.round((value / max) * 100), tone }));
  });

  private loadMasteryValidation(): void {
    this.masteryLoading.set(true);
    this.masteryError.set('');
    this.adminApi.getMasteryValidationSummary().subscribe({
      next: m => { this.masteryValidation.set(m); this.masteryLoading.set(false); },
      error: err => {
        this.masteryError.set(err?.error?.error ?? err?.message ?? 'Failed to load mastery validation summary.');
        this.masteryLoading.set(false);
      },
    });
  }

  refreshMasteryValidation(): void { this.loadMasteryValidation(); }

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
}
