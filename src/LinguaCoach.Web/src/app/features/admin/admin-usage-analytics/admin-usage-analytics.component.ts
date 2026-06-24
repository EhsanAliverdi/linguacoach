import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule, DecimalPipe } from '@angular/common';
import { AdminApiService } from '../../../core/services/admin.api.service';
import {
  AdminDashboardActivityTrendResponse,
  AdminAiUsageTrendResponse,
  ActivityTrendBucket,
  AdminAggAiUsageTrendBucket,
} from '../../../core/models/admin.models';
import {
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminKpiCardComponent,
  SpAdminGraphCardComponent,
  SpAdminLoadingStateComponent,
} from '../../../design-system/admin';
import { SpAdminMiniBarChartComponent, MiniBarItem } from '../../../design-system/admin/components/mini-bar-chart/sp-admin-mini-bar-chart.component';
import { SpAdminVisualPlaceholderComponent } from '../../../design-system/admin/components/visual-placeholder/sp-admin-visual-placeholder.component';

type Period = '7d' | '30d' | '90d';

@Component({
  selector: 'app-admin-usage-analytics',
  standalone: true,
  imports: [
    CommonModule,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminKpiCardComponent,
    SpAdminGraphCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminMiniBarChartComponent,
    SpAdminVisualPlaceholderComponent,
  ],
  providers: [DecimalPipe],
  templateUrl: './admin-usage-analytics.component.html',
  styles: [`
    .sp-ua-period-pills { display: flex; gap: 4px; margin-bottom: 20px; }
    .sp-ua-pill { padding: 5px 14px; font-size: 13px; font-weight: 500; border: 1px solid var(--sp-admin-border,#ECE9F5); border-radius: 20px; background: transparent; color: var(--sp-admin-muted,#6b7280); cursor: pointer; transition: background 0.12s, color 0.12s; }
    .sp-ua-pill--active { background: var(--sp-admin-primary,#5B4BE8); color: #fff; border-color: var(--sp-admin-primary,#5B4BE8); }
    .sp-ua-kpi-strip { display: grid; grid-template-columns: repeat(2,1fr); gap: 14px; margin-bottom: 24px; }
    @media(min-width:900px) { .sp-ua-kpi-strip { grid-template-columns: repeat(4,1fr); } }
    .sp-ua-graph-row { display: grid; gap: 16px; margin-bottom: 24px; }
    @media(min-width:700px) { .sp-ua-graph-row { grid-template-columns: 1fr 1fr; } }
  `],
})
export class AdminUsageAnalyticsComponent implements OnInit {
  readonly periods: { label: string; value: Period }[] = [
    { label: '7 days', value: '7d' },
    { label: '30 days', value: '30d' },
    { label: '90 days', value: '90d' },
  ];

  period = signal<Period>('30d');

  loadingAiTrends = signal(true);
  aiTrendsError = signal('');
  aiTrends = signal<AdminAggAiUsageTrendBucket[]>([]);

  loadingActivityTrends = signal(true);
  activityTrendsError = signal('');
  activityTrends = signal<ActivityTrendBucket[]>([]);

  totalCost = computed(() => this.aiTrends().reduce((s, b) => s + b.cost, 0));
  totalCalls = computed(() => this.aiTrends().reduce((s, b) => s + b.requestCount, 0));
  avgCostPerStudent = computed(() => this.totalCost() / Math.max(1, 8));
  totalActivities = computed(() => this.activityTrends().reduce((s, b) => s + b.activityCount, 0));

  totalCostDisplay = computed(() => '$' + this.totalCost().toFixed(2));
  totalCallsDisplay = computed(() => this.totalCalls().toLocaleString());
  avgCostDisplay = computed(() => '$' + this.avgCostPerStudent().toFixed(2));
  totalActivitiesDisplay = computed(() => this.totalActivities().toLocaleString());
  periodLabel = computed(() => this.periods.find(p => p.value === this.period())?.label ?? '');

  aiTrendItems = computed<MiniBarItem[]>(() =>
    this.aiTrends().slice(-14).map(b => ({ label: b.date.slice(5), value: b.cost }))
  );

  activityTrendItems = computed<MiniBarItem[]>(() =>
    this.activityTrends().slice(-14).map(b => ({ label: b.date.slice(5), value: b.activityCount }))
  );

  constructor(private api: AdminApiService) {}

  ngOnInit(): void {
    this.load();
  }

  setPeriod(p: Period): void {
    this.period.set(p);
    this.load();
  }

  private load(): void {
    const p = this.period();

    this.loadingAiTrends.set(true);
    this.aiTrendsError.set('');
    this.api.getAiUsageTrends(p).subscribe({
      next: (r: AdminAiUsageTrendResponse) => {
        this.aiTrends.set(r.buckets ?? []);
        this.loadingAiTrends.set(false);
      },
      error: () => {
        this.aiTrendsError.set('Backend not available yet.');
        this.loadingAiTrends.set(false);
      },
    });

    this.loadingActivityTrends.set(true);
    this.activityTrendsError.set('');
    this.api.getDashboardActivityTrends(p).subscribe({
      next: (r: AdminDashboardActivityTrendResponse) => {
        this.activityTrends.set(r.buckets ?? []);
        this.loadingActivityTrends.set(false);
      },
      error: () => {
        this.activityTrendsError.set('Backend not available yet.');
        this.loadingActivityTrends.set(false);
      },
    });
  }
}
