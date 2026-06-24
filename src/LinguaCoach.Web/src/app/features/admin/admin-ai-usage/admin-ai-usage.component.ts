import { Component, OnInit, computed, signal, inject } from '@angular/core';
import { CommonModule, DOCUMENT } from '@angular/common';

export type PeriodPreset = 'all' | 'today' | '7d' | '30d' | 'month' | 'custom';
import { FormsModule } from '@angular/forms';
import { AiUsageService, AiUsageSummary, AiUsageRecentItem, AiUsageDateRange, AiUsageRecentCallFilter, AiUsageTrendBucket } from '../../../core/services/ai-usage.service';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { StudentListItem, AdminAiUsageTrendResponse, AdminAiUsageCategoryBreakdownResponse } from '../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCodePillComponent,
  SpAdminCopyableTextComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent,
  SpAdminTableComponent,
  SpAdminTruncatedTextComponent,
} from '../../../design-system/admin';
import { SpAdminMiniBarChartComponent, MiniBarItem } from '../../../design-system/admin/components/mini-bar-chart/sp-admin-mini-bar-chart.component';
import { SpAdminVisualPlaceholderComponent } from '../../../design-system/admin/components/visual-placeholder/sp-admin-visual-placeholder.component';
import { SpAdminRingMetricComponent } from '../../../design-system/admin/components/ring-metric/sp-admin-ring-metric.component';
import { SpAdminBreakdownBarsComponent, BreakdownBarItem } from '../../../design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component';
import { SpAdminGraphCardComponent } from '../../../design-system/admin/components/graph-card/sp-admin-graph-card.component';

@Component({
  selector: 'app-admin-ai-usage',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCodePillComponent,
    SpAdminCopyableTextComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminTableComponent,
    SpAdminTruncatedTextComponent,
    SpAdminMiniBarChartComponent,
    SpAdminVisualPlaceholderComponent,
    SpAdminRingMetricComponent,
    SpAdminBreakdownBarsComponent,
    SpAdminGraphCardComponent,
  ],
  templateUrl: './admin-ai-usage.component.html',
  styles: [`
    .sp-au-kpi-strip { display: grid; grid-template-columns: repeat(2, 1fr); gap: 14px; margin-bottom: 24px; }
    @media(min-width: 900px) { .sp-au-kpi-strip { grid-template-columns: repeat(4, 1fr); } }
    .sp-au-two-col { display: grid; gap: 24px; margin-bottom: 24px; }
    @media(min-width: 1100px) { .sp-au-two-col { grid-template-columns: 1fr 1fr; align-items: start; } }
    .sp-au-num    { text-align: right; white-space: nowrap; }
    .sp-au-num-th { text-align: right; }
    .sp-au-mono   { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; font-size: 12px; }
    .sp-au-muted  { color: var(--sp-admin-muted, #8B85A0); }
    .sp-au-time   { white-space: nowrap; font-size: 12px; }
    .sp-au-feature { min-width: 140px; }
    .sp-au-key    { margin-top: 2px; }
    .sp-au-model  { margin-top: 2px; }
    .sp-au-badges { display: flex; flex-wrap: wrap; gap: 3px; }
    .sp-au-fail-reason { font-size: 11px; color: var(--sp-admin-danger, #dc2626); margin-top: 2px; max-width: 180px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .sp-au-filter-note { font-size: 12px; color: var(--sp-admin-muted, #8B85A0); margin: 0 0 8px; }
    .sp-au-trend-zero td { color: var(--sp-admin-muted, #8B85A0); }
    .sp-au-period-pills { display: flex; gap: 6px; flex-wrap: wrap; margin-bottom: 8px; }
    .sp-au-pill { padding: 4px 14px; border-radius: 99px; font-size: 13px; font-weight: 500; cursor: pointer; border: 1.5px solid var(--sp-admin-border, #E2DEF0); background: transparent; color: var(--sp-admin-muted, #8B85A0); transition: background 0.12s, color 0.12s; }
    .sp-au-pill:hover { background: var(--sp-admin-surface-subtle, #F6F4FB); }
    .sp-au-pill--active { background: var(--sp-admin-primary, #5B4BE8); color: #fff; border-color: var(--sp-admin-primary, #5B4BE8); }
    .sp-au-mini-bars { display: flex; align-items: flex-end; gap: 3px; height: 48px; }
    .sp-au-mini-bar { flex: 1; min-width: 3px; border-radius: 2px 2px 0 0; background: var(--sp-admin-primary, #5B4BE8); opacity: 0.75; transition: height 0.2s; }
    .sp-au-chart-empty { font-size: 12px; color: var(--sp-admin-muted, #8B85A0); padding: 8px 0; }
    .sp-au-not-impl { font-size: 13px; color: var(--sp-admin-muted, #8B85A0); padding: 12px 0; }
    .sp-au-visual-row { display: grid; gap: 16px; grid-template-columns: repeat(2, 1fr); margin-bottom: 24px; }
    @media(min-width: 1100px) { .sp-au-visual-row { grid-template-columns: repeat(3, 1fr); } }
  `],
})
export class AdminAiUsageComponent implements OnInit {
  summary = signal<AiUsageSummary | null>(null);
  recentItems = signal<AiUsageRecentItem[]>([]);
  loadingSummary = signal(true);
  loadingRecent = signal(true);
  summaryError = signal('');
  recentError = signal('');
  recentPage = signal(1);
  readonly recentPageSize = 25;
  recentTotalCount = signal(0);
  recentTotalPages = signal(1);

  periodPreset = signal<PeriodPreset>('all');
  periodPresetValue: PeriodPreset = 'all';

  readonly periodOptions = [
    { value: 'all',    label: 'All time' },
    { value: 'today',  label: 'Today' },
    { value: '7d',     label: 'Last 7 days' },
    { value: '30d',    label: 'Last 30 days' },
    { value: 'month',  label: 'This month' },
    { value: 'custom', label: 'Custom range' },
  ];

  // Custom date range (yyyy-MM-dd strings from <input type="date">)
  customFrom = signal('');
  customTo = signal('');
  customFromValue = '';
  customToValue = '';
  customRangeError = signal('');

  // Server-side recent-call filters
  recentProviderFilter = signal('');
  recentModelFilter = signal('');
  recentFeatureFilter = signal('');
  recentStatusFilter = signal('');
  recentStudentFilter = signal('');

  // Two-way bound values for sp-admin-select
  recentProviderFilterValue = '';
  recentModelFilterValue = '';
  recentFeatureFilterValue = '';
  recentStatusFilterValue = '';
  recentStudentFilterValue = '';

  // Export state
  exporting = signal(false);
  exportError = signal('');

  // Trend state
  trendBuckets = signal<AiUsageTrendBucket[]>([]);
  loadingTrends = signal(false);
  trendError = signal('');

  // Aggregate analytics signals
  aggTrends = signal<AdminAiUsageTrendResponse | null>(null);
  loadingAggTrends = signal(true);
  aggTrendsError = signal(false);
  aggCategoryBreakdown = signal<AdminAiUsageCategoryBreakdownResponse | null>(null);
  loadingAggCategory = signal(true);
  aggCategoryError = signal(false);

  // Student options loaded on init from admin student list
  studentOptions = signal<{ value: string; label: string }[]>([]);

  readonly recentStatusOptions = [
    { value: 'success',  label: 'Success' },
    { value: 'failed',   label: 'Failed' },
    { value: 'fallback', label: 'Fallback' },
  ];

  // Provider options derived from summary byProvider (populated after summary loads)
  providerOptions = computed(() => {
    const fromSummary = (this.summary()?.byProvider ?? []).map(p => p.provider);
    const fromItems   = this.recentItems().map(i => i.provider);
    const all = new Set([...fromSummary, ...fromItems]);
    return Array.from(all).sort().map(p => ({ value: p, label: p }));
  });

  // Model options derived from loaded recent items
  modelOptions = computed(() => {
    const models = new Set(this.recentItems().map(i => i.model).filter(Boolean));
    return Array.from(models).sort().map(m => ({ value: m, label: m }));
  });

  // Feature options derived from summary byFeature
  featureOptions = computed(() => {
    const fromSummary = (this.summary()?.byFeature ?? []).map(f => f.feature);
    const fromItems   = this.recentItems().map(i => i.featureKey);
    const all = new Set([...fromSummary, ...fromItems]);
    return Array.from(all).sort().map(f => ({ value: f, label: this.featureLabel(f) }));
  });

  // Items already filtered server-side; expose directly for template
  filteredRecentItems = computed(() => this.recentItems());

  private readonly doc = inject<Document>(DOCUMENT);

  constructor(private svc: AiUsageService, private adminApi: AdminApiService) {}

  ngOnInit(): void {
    this.loadStudentOptions();
    this.load();
    this.loadAggregateAnalytics();
  }

  private loadAggregateAnalytics(): void {
    this.adminApi.getAiUsageTrends('30d').subscribe({
      next: r => { this.aggTrends.set(r); this.loadingAggTrends.set(false); },
      error: () => { this.aggTrendsError.set(true); this.loadingAggTrends.set(false); },
    });
    this.adminApi.getAiUsageCategoryBreakdown('30d').subscribe({
      next: r => { this.aggCategoryBreakdown.set(r); this.loadingAggCategory.set(false); },
      error: () => { this.aggCategoryError.set(true); this.loadingAggCategory.set(false); },
    });
  }

  private loadStudentOptions(): void {
    this.adminApi.listStudents({ pageSize: 50 }).subscribe({
      next: r => {
        this.studentOptions.set(
          r.items.map((s: StudentListItem) => ({
            value: s.studentProfileId,
            label: s.displayName ? `${s.displayName} (${s.email})` : s.email,
          }))
        );
      },
      error: () => { /* silently ignore — student filter will just be empty */ },
    });
  }

  exportCsv(): void {
    if (this.exporting()) return;
    this.exporting.set(true);
    this.exportError.set('');
    const range   = this.buildRange(this.periodPreset());
    const filters = this.buildColumnFilters();
    this.svc.exportUsageCsv(range, filters).subscribe({
      next: blob => {
        const url  = URL.createObjectURL(blob);
        const link = this.doc.createElement('a');
        const now  = new Date();
        const ts   = `${now.getFullYear()}${String(now.getMonth()+1).padStart(2,'0')}${String(now.getDate()).padStart(2,'0')}-${String(now.getHours()).padStart(2,'0')}${String(now.getMinutes()).padStart(2,'0')}${String(now.getSeconds()).padStart(2,'0')}`;
        link.href     = url;
        link.download = `ai-usage-${ts}.csv`;
        link.click();
        URL.revokeObjectURL(url);
        this.exporting.set(false);
      },
      error: err => {
        this.exportError.set(err.error?.error ?? 'Export failed. Please try again.');
        this.exporting.set(false);
      },
    });
  }

  load(): void {
    const range = this.buildRange(this.periodPreset());
    const filters = this.buildColumnFilters();
    this.loadingSummary.set(true);
    this.summaryError.set('');

    this.svc.getSummary(range, filters).subscribe({
      next: s => { this.summary.set(s); this.loadingSummary.set(false); },
      error: err => { this.summaryError.set(err.error?.error ?? 'Could not load summary.'); this.loadingSummary.set(false); },
    });
    this.loadRecent();
    this.loadTrends();
  }

  private buildColumnFilters(): AiUsageRecentCallFilter {
    return {
      provider:   this.recentProviderFilter()  || undefined,
      model:      this.recentModelFilter()     || undefined,
      featureKey: this.recentFeatureFilter()   || undefined,
      status:     this.recentStatusFilter()    || undefined,
      studentId:  this.recentStudentFilter()   || undefined,
    };
  }

  loadRecent(): void {
    const range   = this.buildRange(this.periodPreset());
    const filters = this.buildColumnFilters();
    this.loadingRecent.set(true);
    this.recentError.set('');

    this.svc.getRecent(this.recentPage(), this.recentPageSize, range, filters).subscribe({
      next: r => {
        this.recentItems.set(r.items);
        this.recentTotalCount.set(r.totalCount);
        this.recentTotalPages.set(r.totalPages);
        this.loadingRecent.set(false);
      },
      error: err => { this.recentError.set(err.error?.error ?? 'Could not load recent calls.'); this.loadingRecent.set(false); },
    });
  }

  loadTrends(): void {
    const range   = this.buildRange(this.periodPreset());
    const filters = this.buildColumnFilters();
    this.loadingTrends.set(true);
    this.trendError.set('');
    this.svc.getTrends(range, filters).subscribe({
      next: buckets => { this.trendBuckets.set(buckets); this.loadingTrends.set(false); },
      error: err    => { this.trendError.set(err.error?.error ?? 'Could not load trends.'); this.loadingTrends.set(false); },
    });
  }

  onPeriodChange(value: PeriodPreset): void {
    this.periodPreset.set(value);
    this.recentPage.set(1);
    if (value !== 'custom') {
      this.customRangeError.set('');
      this.load();
    }
  }

  applyCustomRange(): void {
    const from = this.customFrom();
    const to   = this.customTo();
    if (!from || !to) {
      this.customRangeError.set('Both From and To dates are required.');
      return;
    }
    if (from > to) {
      this.customRangeError.set('From date must be on or before To date.');
      return;
    }
    this.customRangeError.set('');
    this.recentPage.set(1);
    this.load();
  }

  clearCustomRange(): void {
    this.customFrom.set('');
    this.customTo.set('');
    this.customFromValue = '';
    this.customToValue = '';
    this.customRangeError.set('');
  }

  onRecentPageChange(page: number): void {
    this.recentPage.set(page);
    this.loadRecent();
  }

  onRecentProviderChange(value: string): void {
    this.recentProviderFilter.set(value);
    this.recentPage.set(1);
    this.load();
  }

  onRecentModelChange(value: string): void {
    this.recentModelFilter.set(value);
    this.recentPage.set(1);
    this.load();
  }

  onRecentFeatureChange(value: string): void {
    this.recentFeatureFilter.set(value);
    this.recentPage.set(1);
    this.load();
  }

  onRecentStatusChange(value: string): void {
    this.recentStatusFilter.set(value);
    this.recentPage.set(1);
    this.load();
  }

  onRecentStudentChange(value: string): void {
    this.recentStudentFilter.set(value);
    this.recentPage.set(1);
    this.load();
  }

  clearRecentFilters(): void {
    this.recentProviderFilter.set('');
    this.recentModelFilter.set('');
    this.recentFeatureFilter.set('');
    this.recentStatusFilter.set('');
    this.recentStudentFilter.set('');
    this.recentProviderFilterValue = '';
    this.recentModelFilterValue = '';
    this.recentFeatureFilterValue = '';
    this.recentStatusFilterValue = '';
    this.recentStudentFilterValue = '';
    this.recentPage.set(1);
    this.load();
  }

  hasActiveRecentFilters = computed(() =>
    !!this.recentProviderFilter() ||
    !!this.recentModelFilter()    ||
    !!this.recentFeatureFilter()  ||
    !!this.recentStatusFilter()   ||
    !!this.recentStudentFilter());

  // KPI strip summary (4 tiles: total calls, total cost, success rate, failed)
  readonly kpiSummary = computed(() => {
    const s = this.summary();
    if (!s) return null;
    return {
      totalCalls:   s.totalCalls,
      totalCostUsd: s.totalCostUsd,
      successRate:  s.totalCalls > 0 ? Math.round((s.successfulCalls / s.totalCalls) * 100) : 0,
      failedCalls:  s.failedCalls,
    };
  });

  // Mini bar chart heights for trend (proportional 0-48 px) — kept for backward compat
  readonly trendBars = computed(() => {
    const buckets = this.trendBuckets();
    if (!buckets.length) return [];
    const max = Math.max(...buckets.map(b => b.callCount));
    if (max === 0) return buckets.map(() => ({ height: 2, label: '0' }));
    return buckets.map(b => ({
      height: Math.max(2, Math.round((b.callCount / max) * 48)),
      label:  `${b.date}: ${b.callCount} calls`,
    }));
  });

  // MiniBarItem[] for sp-admin-mini-bar-chart
  readonly trendItems = computed<MiniBarItem[]>(() =>
    this.trendBuckets().map(b => ({
      label: b.date.slice(5), // MM-DD
      value: b.callCount,
      date: b.date,
    }))
  );

  readonly successRingPct = computed<number>(() => {
    const s = this.summary();
    if (!s || s.totalCalls === 0) return 0;
    return Math.round((s.successfulCalls / s.totalCalls) * 100);
  });

  readonly tokenBreakdownItems = computed<BreakdownBarItem[]>(() => {
    const s = this.summary();
    if (!s || s.totalTokens === 0) return [];
    const total = s.totalTokens;
    return [
      { label: 'Input tokens',  value: s.totalInputTokens,  pct: Math.round((s.totalInputTokens  / total) * 100), tone: 'indigo' },
      { label: 'Output tokens', value: s.totalOutputTokens, pct: Math.round((s.totalOutputTokens / total) * 100), tone: 'violet' },
    ];
  });

  readonly providerBreakdownItems = computed<BreakdownBarItem[]>(() => {
    const s = this.summary();
    if (!s) return [];
    const total = s.totalCalls || 1;
    const tones: BreakdownBarItem['tone'][] = ['indigo', 'violet', 'teal', 'amber', 'green'];
    return s.byProvider.map((p, i) => ({
      label: p.provider,
      value: p.calls,
      pct: Math.round((p.calls / total) * 100),
      tone: tones[i % tones.length],
    }));
  });

  readonly aggTrendItems = computed<BreakdownBarItem[]>(() => {
    const data = this.aggTrends();
    if (!data) return [];
    const max = Math.max(...data.buckets.map(b => b.requestCount), 1);
    return data.buckets.slice(-7).map(b => ({
      label: b.date.slice(5),
      value: b.requestCount,
      pct: Math.round((b.requestCount / max) * 100),
      tone: 'indigo' as const,
    }));
  });

  readonly aggCategoryItems = computed<BreakdownBarItem[]>(() => {
    const data = this.aggCategoryBreakdown();
    if (!data) return [];
    const max = Math.max(...data.categories.map(c => c.requestCount), 1);
    const tones: BreakdownBarItem['tone'][] = ['indigo', 'violet', 'teal', 'amber', 'green', 'slate'];
    return data.categories.map((c, i) => ({
      label: c.category.replace(/_/g, ' '),
      value: c.requestCount,
      pct: Math.round((c.requestCount / max) * 100),
      tone: tones[i % tones.length],
    }));
  });

  readonly periodPillOptions: { value: PeriodPreset; label: string }[] = [
    { value: '7d',  label: '7d' },
    { value: '30d', label: '30d' },
    { value: 'all', label: 'All' },
  ];

  onPillClick(value: PeriodPreset): void {
    this.periodPresetValue = value;
    this.onPeriodChange(value);
  }


  buildRange(preset: PeriodPreset): AiUsageDateRange | undefined {
    const now = new Date();
    switch (preset) {
      case 'today': {
        const start = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
        return { from: start.toISOString() };
      }
      case '7d': {
        const start = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);
        return { from: start.toISOString() };
      }
      case '30d': {
        const start = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
        return { from: start.toISOString() };
      }
      case 'month': {
        const start = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
        return { from: start.toISOString() };
      }
      case 'custom': {
        const from = this.customFrom();
        const to   = this.customTo();
        if (!from || !to) return undefined;
        // From: start of the chosen day UTC. To: start of the day after (exclusive upper bound).
        const fromDate = new Date(from + 'T00:00:00Z');
        const [y, m, d] = to.split('-').map(Number);
        const toDate = new Date(Date.UTC(y, m - 1, d + 1));
        return { from: fromDate.toISOString(), to: toDate.toISOString() };
      }
      default:
        return undefined;
    }
  }

  formatDateTime(iso: string): string {
    try {
      const d = new Date(iso);
      const date = d.toLocaleDateString('en-AU', { day: '2-digit', month: 'short' });
      const time = d.toLocaleTimeString('en-AU', { hour12: false, hour: '2-digit', minute: '2-digit', second: '2-digit' });
      return `${date} ${time}`;
    } catch { return iso; }
  }

  featureLabel(key: string): string {
    return key.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
  }

  formatTokens(n: number): string {
    return n.toLocaleString();
  }
}
