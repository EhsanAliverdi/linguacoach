import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminErrorStateComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
} from '../../../design-system/admin';
import { SpAdminRingMetricComponent } from '../../../design-system/admin/components/ring-metric/sp-admin-ring-metric.component';
import { SpAdminBreakdownBarsComponent, BreakdownBarItem } from '../../../design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component';
import { SpAdminGraphCardComponent } from '../../../design-system/admin/components/graph-card/sp-admin-graph-card.component';
import { SpAdminAreaChartComponent } from '../../../design-system/admin/components/area-chart/sp-admin-area-chart.component';
import { SpAdminVisualPlaceholderComponent } from '../../../design-system/admin/components/visual-placeholder/sp-admin-visual-placeholder.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AdminDeliveryHealth, MasteryValidationSummary } from '../../../core/models/admin.models';

@Component({
  selector: 'app-admin-delivery-health',
  standalone: true,
  templateUrl: './admin-delivery-health.component.html',
  imports: [
    CommonModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminErrorStateComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminRingMetricComponent,
    SpAdminBreakdownBarsComponent,
    SpAdminGraphCardComponent,
    SpAdminAreaChartComponent,
    SpAdminVisualPlaceholderComponent,
  ],
})
export class AdminDeliveryHealthComponent implements OnInit {
  constructor(private adminApi: AdminApiService) {}

  readonly lookbackDays = 7;

  // ── Today Plan pipeline ─────────────────────────────────────────────────
  todayHealth = signal<AdminDeliveryHealth | null>(null);
  todayLoading = signal(true);
  todayError = signal('');

  // ── Practice Gym pipeline ───────────────────────────────────────────────
  practiceGymHealth = signal<AdminDeliveryHealth | null>(null);
  practiceGymLoading = signal(true);
  practiceGymError = signal('');

  // ── Mastery validation (system-wide diagnostic, unchanged) ─────────────
  masteryLoading = signal(false);
  masteryError = signal('');
  masteryValidation = signal<MasteryValidationSummary | null>(null);

  ngOnInit(): void {
    this.loadTodayHealth();
    this.loadPracticeGymHealth();
    this.loadMasteryValidation();
  }

  private loadTodayHealth(): void {
    this.todayLoading.set(true);
    this.todayError.set('');
    this.adminApi.getTodayPlanDeliveryHealth(this.lookbackDays).subscribe({
      next: h => { this.todayHealth.set(h); this.todayLoading.set(false); },
      error: err => {
        this.todayError.set(err?.error?.error ?? err?.message ?? 'Failed to load Today delivery health.');
        this.todayLoading.set(false);
      },
    });
  }

  private loadPracticeGymHealth(): void {
    this.practiceGymLoading.set(true);
    this.practiceGymError.set('');
    this.adminApi.getPracticeGymDeliveryHealth(this.lookbackDays).subscribe({
      next: h => { this.practiceGymHealth.set(h); this.practiceGymLoading.set(false); },
      error: err => {
        this.practiceGymError.set(err?.error?.error ?? err?.message ?? 'Failed to load Practice Gym delivery health.');
        this.practiceGymLoading.set(false);
      },
    });
  }

  refreshTodayHealth(): void { this.loadTodayHealth(); }
  refreshPracticeGymHealth(): void { this.loadPracticeGymHealth(); }

  todaySelectedRatePct = computed(() => this.selectedRatePct(this.todayHealth()));
  practiceGymSelectedRatePct = computed(() => this.selectedRatePct(this.practiceGymHealth()));

  private selectedRatePct(health: AdminDeliveryHealth | null): number {
    if (!health) return 0;
    const denom = health.today.selectedCount + health.today.fallbackOnlyCount;
    return denom > 0 ? Math.round((health.today.selectedCount / denom) * 100) : 0;
  }

  todayCefrBreakdown = computed<BreakdownBarItem[]>(() => this.cefrBreakdown(this.todayHealth()));
  practiceGymCefrBreakdown = computed<BreakdownBarItem[]>(() => this.cefrBreakdown(this.practiceGymHealth()));

  private cefrBreakdown(health: AdminDeliveryHealth | null): BreakdownBarItem[] {
    const buckets = health?.byCefrLevel ?? [];
    const max = Math.max(...buckets.map(b => b.selectedCount + b.fallbackOnlyCount), 1);
    return buckets
      .filter(b => b.eligibleStudents > 0)
      .map(b => ({
        label: b.cefrLevel,
        value: b.selectedCount,
        pct: Math.round(((b.selectedCount + b.fallbackOnlyCount) / max) * 100),
        tone: b.fallbackOnlyCount > b.selectedCount ? 'amber' as const : 'indigo' as const,
      }));
  }

  todayTrendValues = computed<number[]>(() => (this.todayHealth()?.trend ?? []).map(t => t.selectedCount));
  todayTrendLabels = computed<string[]>(() => (this.todayHealth()?.trend ?? []).map(t => t.date.slice(5)));
  practiceGymTrendValues = computed<number[]>(() => (this.practiceGymHealth()?.trend ?? []).map(t => t.selectedCount));
  practiceGymTrendLabels = computed<string[]>(() => (this.practiceGymHealth()?.trend ?? []).map(t => t.date.slice(5)));

  todayBankGaps = computed(() => (this.todayHealth()?.bankCoverage ?? []).filter(b => b.approvedModuleCount === 0));
  practiceGymBankGaps = computed(() => (this.practiceGymHealth()?.bankCoverage ?? []).filter(b => b.approvedModuleCount === 0));

  // ── Mastery validation — chart data (unchanged) ─────────────────────────

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
}
