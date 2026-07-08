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
import { AdminGenerationBatchesResponse, AggregatePoolHealthSummary, ReviewScaffoldDryRunSummary, ReviewScaffoldItemDetail, ReviewScaffoldPilotSummary, MasteryValidationSummary } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-lessons',
  standalone: true,
  templateUrl: './admin-lessons.component.html',
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

  // ── Review scaffold pending admin review ──────────────────────────────────
  scaffoldPendingLoading = signal(false);
  scaffoldPendingError = signal('');
  scaffoldPending = signal<ReviewScaffoldItemDetail[]>([]);
  scaffoldActionPendingId = signal<string | null>(null);
  scaffoldActionError = signal('');

  // ── Phase 19C: Practice Gym review scaffold pilot monitoring ──────────────
  pilotSummaryLoading = signal(false);
  pilotSummaryError = signal('');
  pilotSummary = signal<ReviewScaffoldPilotSummary | null>(null);

  pilotStatusItems = computed<BreakdownBarItem[]>(() => {
    const p = this.pilotSummary();
    if (!p) return [];
    const values: [string, number, BreakdownBarItem['tone']][] = [
      ['Student-visible now', p.studentVisibleCount, 'green'],
      ['Approved (total)', p.approvedCount, 'indigo'],
      ['Pending review', p.pendingReviewCount, 'amber'],
      ['Rejected', p.rejectedCount, 'slate'],
      ['Consumed', p.consumedCount, 'teal'],
      ['Skipped / expired', p.skippedOrExpiredCount, 'danger'],
    ];
    const max = Math.max(...values.map(v => v[1]), 1);
    return values
      .filter(([, value]) => value > 0)
      .map(([label, value, tone]) => ({ label, value, pct: Math.round((value / max) * 100), tone }));
  });

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
    this.loadPoolHealth();
    this.loadScaffoldDryRun();
    this.loadScaffoldPendingReview();
    this.loadPilotSummary();
    this.loadMasteryValidation();
  }

  private loadPilotSummary(): void {
    this.pilotSummaryLoading.set(true);
    this.pilotSummaryError.set('');
    this.adminApi.getReviewScaffoldPilotSummary().subscribe({
      next: p => { this.pilotSummary.set(p); this.pilotSummaryLoading.set(false); },
      error: err => {
        this.pilotSummaryError.set(err?.error?.error ?? err?.message ?? 'Failed to load pilot summary.');
        this.pilotSummaryLoading.set(false);
      },
    });
  }

  refreshPilotSummary(): void { this.loadPilotSummary(); }

  // ── Pool health — chart data ───────────────────────────────────────────────

  poolReadyRingPct = computed<number>(() => {
    const h = this.poolHealth();
    if (!h || h.totalStudentsWithItems === 0) return 0;
    return Math.round(((h.totalStudentsWithItems - h.studentsWithNoReadyItems) / h.totalStudentsWithItems) * 100);
  });

  poolStatusItems = computed<BreakdownBarItem[]>(() => {
    const h = this.poolHealth();
    if (!h) return [];
    const queuedOrGenerating = h.totalQueued + h.totalGenerating;
    const staleOrExpired = h.totalStale + h.totalExpired;
    const total = h.totalReady + h.totalReserved + queuedOrGenerating
      + h.totalReviewOnly + staleOrExpired + h.totalFailed + h.totalSkipped;
    if (total === 0) return [];
    const mk = (label: string, value: number, tone: BreakdownBarItem['tone']): BreakdownBarItem =>
      ({ label, value, pct: Math.round((value / total) * 100), tone });
    return [
      mk('Ready', h.totalReady, 'green'),
      mk('Reserved', h.totalReserved, 'indigo'),
      mk('Queued / generating', queuedOrGenerating, 'teal'),
      mk('Review only', h.totalReviewOnly, 'violet'),
      mk('Stale / expired', staleOrExpired, 'amber'),
      mk('Failed', h.totalFailed, 'danger'),
      mk('Skipped', h.totalSkipped, 'slate'),
    ].filter(i => i.value > 0);
  });

  poolAttentionItems = computed<BreakdownBarItem[]>(() => {
    const h = this.poolHealth();
    if (!h || h.totalStudentsWithItems === 0) return [];
    const total = h.totalStudentsWithItems;
    const mk = (label: string, value: number, tone: BreakdownBarItem['tone']): BreakdownBarItem =>
      ({ label, value, pct: Math.min(100, Math.round((value / total) * 100)), tone });
    return [
      mk('No ready items', h.studentsWithNoReadyItems, 'danger'),
      mk('Below minimum threshold', h.studentsBelowMinimumThreshold, 'amber'),
      mk('Has failed items', h.studentsWithFailedItems, 'amber'),
      mk('Has stale items', h.studentsWithStaleItems, 'slate'),
    ].filter(i => i.value > 0);
  });

  // ── Review scaffold — funnel chart data ────────────────────────────────────

  scaffoldFunnelItems = computed<BreakdownBarItem[]>(() => {
    const s = this.scaffoldDryRun();
    if (!s) return [];
    const values: [string, number, BreakdownBarItem['tone']][] = [
      ['Eligible for review', s.studentsEligibleForReview, 'indigo'],
      ['Net new review items (est.)', s.estimatedNetNewReviewItems, 'green'],
      ['Blocked (duplicate)', s.blockedDuplicates, 'amber'],
      ['Blocked (inactive objective)', s.blockedInactiveObjectives, 'slate'],
      ['Held for admin review', s.adminReviewRequiredCount, 'violet'],
      ['Generated today', s.generatedTodayCount, 'teal'],
    ];
    const max = Math.max(...values.map(v => v[1]), 1);
    return values
      .filter(([, value]) => value > 0)
      .map(([label, value, tone]) => ({ label, value, pct: Math.round((value / max) * 100), tone }));
  });

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

  private loadPoolHealth(): void {
    this.poolHealthLoading.set(true);
    this.poolHealthError.set('');
    this.adminApi.getAggregatePoolHealth().subscribe({
      next: h => { this.poolHealth.set(h); this.poolHealthLoading.set(false); },
      error: err => {
        this.poolHealthError.set(err?.error?.error ?? err?.message ?? 'Failed to load aggregate delivery queue health.');
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

  private loadScaffoldPendingReview(): void {
    this.scaffoldPendingLoading.set(true);
    this.scaffoldPendingError.set('');
    this.adminApi.getReviewScaffoldPendingReview().subscribe({
      next: items => { this.scaffoldPending.set(items); this.scaffoldPendingLoading.set(false); },
      error: err => {
        this.scaffoldPendingError.set(err?.error?.error ?? err?.message ?? 'Failed to load pending review items.');
        this.scaffoldPendingLoading.set(false);
      },
    });
  }

  refreshScaffoldPendingReview(): void { this.loadScaffoldPendingReview(); }

  approveScaffoldItem(item: ReviewScaffoldItemDetail): void {
    if (!confirm(`Approve this review scaffold item for student ${item.studentId}?`)) return;
    this.scaffoldActionError.set('');
    this.scaffoldActionPendingId.set(item.id);
    this.adminApi.approveReviewScaffoldItem(item.id).subscribe({
      next: () => {
        this.scaffoldActionPendingId.set(null);
        this.loadScaffoldPendingReview();
        this.loadPilotSummary();
      },
      error: err => {
        this.scaffoldActionError.set(err?.error?.error ?? err?.message ?? 'Approve failed.');
        this.scaffoldActionPendingId.set(null);
      },
    });
  }

  rejectScaffoldItem(item: ReviewScaffoldItemDetail): void {
    const reason = window.prompt('Reason for rejecting this review scaffold item:');
    if (reason === null) return;
    if (!reason.trim()) { this.scaffoldActionError.set('A reason is required to reject.'); return; }
    if (!confirm(`Reject this review scaffold item for student ${item.studentId}?`)) return;
    this.scaffoldActionError.set('');
    this.scaffoldActionPendingId.set(item.id);
    this.adminApi.rejectReviewScaffoldItem(item.id, { reason: reason.trim() }).subscribe({
      next: () => {
        this.scaffoldActionPendingId.set(null);
        this.loadScaffoldPendingReview();
        this.loadPilotSummary();
      },
      error: err => {
        this.scaffoldActionError.set(err?.error?.error ?? err?.message ?? 'Reject failed.');
        this.scaffoldActionPendingId.set(null);
      },
    });
  }

  reopenScaffoldItem(item: ReviewScaffoldItemDetail): void {
    if (!confirm(`Reopen this rejected review scaffold item for student ${item.studentId}?`)) return;
    this.scaffoldActionError.set('');
    this.scaffoldActionPendingId.set(item.id);
    this.adminApi.reopenReviewScaffoldItem(item.id).subscribe({
      next: () => {
        this.scaffoldActionPendingId.set(null);
        this.loadScaffoldPendingReview();
      },
      error: err => {
        this.scaffoldActionError.set(err?.error?.error ?? err?.message ?? 'Reopen failed.');
        this.scaffoldActionPendingId.set(null);
      },
    });
  }
}
