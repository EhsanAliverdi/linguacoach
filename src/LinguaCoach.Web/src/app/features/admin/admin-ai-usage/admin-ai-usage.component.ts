import { Component, OnInit, computed, signal } from '@angular/core';

export type PeriodPreset = 'all' | 'today' | '7d' | '30d' | 'month';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AiUsageService, AiUsageSummary, AiUsageRecentItem, AiUsageDateRange } from '../../../core/services/ai-usage.service';
import {
  SpAdminBadgeComponent,
  SpAdminCardComponent,
  SpAdminCodePillComponent,
  SpAdminCopyableTextComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent,
  SpAdminStatCardComponent,
  SpAdminTableComponent,
  SpAdminTruncatedTextComponent,
} from '../../../admin';

@Component({
  selector: 'app-admin-ai-usage',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
    SpAdminCardComponent,
    SpAdminCodePillComponent,
    SpAdminCopyableTextComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminStatCardComponent,
    SpAdminTableComponent,
    SpAdminTruncatedTextComponent,
  ],
  templateUrl: './admin-ai-usage.component.html',
  styles: [`
    .sp-au-stat-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 14px; margin-bottom: 24px; }
    @media(min-width: 900px)  { .sp-au-stat-grid { grid-template-columns: repeat(4, 1fr); } }
    @media(min-width: 1200px) { .sp-au-stat-grid { grid-template-columns: repeat(8, 1fr); } }
    .sp-au-two-col { display: grid; gap: 24px; margin-bottom: 24px; }
    @media(min-width: 1100px) { .sp-au-two-col { grid-template-columns: 1fr 1fr; align-items: start; } }
    .sp-au-num    { text-align: right; white-space: nowrap; }
    .sp-au-num-th { text-align: right; }
    .sp-au-mono   { font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace; font-size: 12px; }
    .sp-au-muted  { color: var(--sp-admin-muted, #9ca3af); }
    .sp-au-time   { white-space: nowrap; font-size: 12px; }
    .sp-au-feature { min-width: 140px; }
    .sp-au-key    { margin-top: 2px; }
    .sp-au-model  { margin-top: 2px; }
    .sp-au-badges { display: flex; flex-wrap: wrap; gap: 3px; }
    .sp-au-fail-reason { font-size: 11px; color: var(--sp-admin-danger, #dc2626); margin-top: 2px; max-width: 180px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
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
    { value: 'all',   label: 'All time' },
    { value: 'today', label: 'Today' },
    { value: '7d',    label: 'Last 7 days' },
    { value: '30d',   label: 'Last 30 days' },
    { value: 'month', label: 'This month' },
  ];

  providerFilter = signal('');
  statusFilter = signal('');
  providerFilterValue = '';
  statusFilterValue = '';

  readonly recentStatusOptions = [
    { value: 'ok',       label: 'OK' },
    { value: 'failed',   label: 'Failed' },
    { value: 'fallback', label: 'Fallback' },
  ];

  providerOptions = computed(() => {
    const providers = new Set<string>();
    for (const item of this.recentItems()) {
      if (item.provider) providers.add(item.provider);
    }
    return Array.from(providers).sort().map(p => ({ value: p, label: p }));
  });

  filteredRecentItems = computed(() => {
    const provider = this.providerFilter();
    const status = this.statusFilter();
    return this.recentItems().filter(item => {
      if (provider && item.provider !== provider) return false;
      if (status === 'ok'       && !item.wasSuccessful)  return false;
      if (status === 'failed'   && item.wasSuccessful)   return false;
      if (status === 'fallback' && !item.isFallback)     return false;
      return true;
    });
  });

  constructor(private svc: AiUsageService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    const range = this.buildRange(this.periodPreset());
    this.loadingSummary.set(true);
    this.summaryError.set('');

    this.svc.getSummary(range).subscribe({
      next: s => { this.summary.set(s); this.loadingSummary.set(false); },
      error: err => { this.summaryError.set(err.error?.error ?? 'Could not load summary.'); this.loadingSummary.set(false); },
    });
    this.loadRecent();
  }

  loadRecent(): void {
    const range = this.buildRange(this.periodPreset());
    this.loadingRecent.set(true);
    this.recentError.set('');

    this.svc.getRecent(this.recentPage(), this.recentPageSize, range).subscribe({
      next: r => {
        this.recentItems.set(r.items);
        this.recentTotalCount.set(r.totalCount);
        this.recentTotalPages.set(r.totalPages);
        this.loadingRecent.set(false);
      },
      error: err => { this.recentError.set(err.error?.error ?? 'Could not load recent calls.'); this.loadingRecent.set(false); },
    });
  }

  onPeriodChange(value: PeriodPreset): void {
    this.periodPreset.set(value);
    this.recentPage.set(1);
    this.load();
  }

  onRecentPageChange(page: number): void {
    this.recentPage.set(page);
    this.loadRecent();
  }

  onProviderFilterChange(value: string): void {
    this.providerFilter.set(value);
    this.recentPage.set(1);
  }

  onStatusFilterChange(value: string): void {
    this.statusFilter.set(value);
    this.recentPage.set(1);
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
