import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  AdminDashboardActivityTrendResponse,
  AdminAiUsageTrendResponse,
  ActivityTrendBucket,
  AdminAggAiUsageTrendBucket,
} from '../../../core/models/admin.models';
import {
  SpAdminCardComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminErrorStateComponent,
  SpAdminNotImplementedStateComponent,
  SpAdminAreaChartComponent,
  SpAdminBarChartComponent,
  SpAdminFlyoutComponent,
} from '../../../design-system/admin';

type Period = 'today' | '7d' | '30d' | 'month' | 'custom';

@Component({
  selector: 'app-admin-usage-analytics',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminCardComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminErrorStateComponent,
    SpAdminNotImplementedStateComponent,
    SpAdminAreaChartComponent,
    SpAdminBarChartComponent,
    SpAdminFlyoutComponent,
  ],
  providers: [DecimalPipe],
  templateUrl: './admin-usage-analytics.component.html',
})
export class AdminUsageAnalyticsComponent implements OnInit {
  readonly periods: { label: string; value: Period }[] = [
    { label: 'Today',     value: 'today'  },
    { label: '7 days',    value: '7d'     },
    { label: '30 days',   value: '30d'    },
    { label: '3 months',  value: 'month'  },
    { label: 'Custom',    value: 'custom' },
  ];

  period = signal<Period>('30d');
  customFrom = signal('');
  customTo   = signal('');
  customFromValue = '';
  customToValue   = '';
  customRangeError = signal('');

  loadingAiTrends    = signal(true);
  aiTrendsError      = signal('');
  aiTrends           = signal<AdminAggAiUsageTrendBucket[]>([]);

  loadingActivityTrends  = signal(true);
  activityTrendsError    = signal('');
  activityTrends         = signal<ActivityTrendBucket[]>([]);

  // ── KPI computed ─────────────────────────────────────────────────
  totalCost        = computed(() => this.aiTrends().reduce((s, b) => s + b.cost, 0));
  totalCalls       = computed(() => this.aiTrends().reduce((s, b) => s + b.requestCount, 0));
  // studentCount comes from the activity-trends buckets (distinct active students not yet available from this endpoint)
  // Using total activities as a proxy denominator is misleading — mark as not-implemented until a student-count endpoint is wired.
  avgCostPerStudent = computed<number | null>(() => null);
  totalActivities  = computed(() => this.activityTrends().reduce((s, b) => s + b.activityCount, 0));

  totalCostDisplay        = computed(() => '$' + this.totalCost().toFixed(2));
  totalCallsDisplay       = computed(() => this.totalCalls().toLocaleString());
  avgCostDisplay          = computed(() => this.avgCostPerStudent() !== null ? '$' + this.avgCostPerStudent()!.toFixed(2) : 'N/A');
  totalActivitiesDisplay  = computed(() => this.totalActivities().toLocaleString());
  periodLabel             = computed(() => this.periods.find(p => p.value === this.period())?.label ?? '');

  // ── Area chart: AI cost ───────────────────────────────────────────
  aiCostChartData = computed<number[]>(() => this.aiTrends().map(b => b.cost));
  aiCostChartLabels = computed<string[]>(() => {
    const buckets = this.aiTrends();
    if (!buckets.length) return [];
    // Show label only at a few evenly spaced indices to avoid crowding
    const n = buckets.length;
    const step = Math.max(1, Math.floor(n / 6));
    return buckets.map((b, i) => (i % step === 0 || i === n - 1) ? b.date.slice(5) : '');
  });

  // ── Bar chart: activities ─────────────────────────────────────────
  activityChartData = computed<number[]>(() => this.activityTrends().slice(-14).map(b => b.activityCount));
  activityChartLabels = computed<string[]>(() => {
    const buckets = this.activityTrends().slice(-14);
    if (!buckets.length) return [];
    const n = buckets.length;
    const step = Math.max(1, Math.floor(n / 5));
    return buckets.map((b, i) => (i % step === 0 || i === n - 1) ? b.date.slice(5) : '');
  });

  constructor(private api: AdminApiService) {}

  ngOnInit(): void { this.load(); }

  setPeriod(p: Period): void {
    this.period.set(p);
    this.customRangeError.set('');
    if (p !== 'custom') this.load();
  }

  applyCustomRange(): void {
    if (!this.customFrom() || !this.customTo()) {
      this.customRangeError.set('Select both a start and end date.');
      return;
    }
    if (this.customFrom() > this.customTo()) {
      this.customRangeError.set('Start date must be before end date.');
      return;
    }
    this.customRangeError.set('');
    this.load();
  }

  private load(): void {
    const p = this.period();
    // For custom, build a range string the API can interpret; fall back to 30d if dates missing
    let apiPeriod = p;
    if (p === 'custom') {
      const from = this.customFrom();
      const to   = this.customTo();
      if (!from || !to) return;
      apiPeriod = `custom:${from}:${to}` as Period;
    }

    this.loadingAiTrends.set(true);
    this.aiTrendsError.set('');
    this.api.getAiUsageTrends(apiPeriod).subscribe({
      next: (r: AdminAiUsageTrendResponse) => { this.aiTrends.set(r.buckets ?? []); this.loadingAiTrends.set(false); },
      error: () => { this.aiTrendsError.set('Backend not available yet.'); this.loadingAiTrends.set(false); },
    });

    this.loadingActivityTrends.set(true);
    this.activityTrendsError.set('');
    this.api.getDashboardActivityTrends(apiPeriod).subscribe({
      next: (r: AdminDashboardActivityTrendResponse) => { this.activityTrends.set(r.buckets ?? []); this.loadingActivityTrends.set(false); },
      error: () => { this.activityTrendsError.set('Backend not available yet.'); this.loadingActivityTrends.set(false); },
    });
  }
}
