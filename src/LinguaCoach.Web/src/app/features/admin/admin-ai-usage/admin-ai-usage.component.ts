import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AiUsageService, AiUsageSummary, AiUsageRecentItem } from '../../../core/services/ai-usage.service';
import {
  SpAdminBadgeComponent,
  SpAdminCardComponent,
  SpAdminCodePillComponent,
  SpAdminCopyableTextComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminStatCardComponent,
  SpAdminTableComponent,
  SpAdminTruncatedTextComponent,
} from '../../../admin';

@Component({
  selector: 'app-admin-ai-usage',
  standalone: true,
  imports: [
    CommonModule,
    SpAdminBadgeComponent,
    SpAdminCardComponent,
    SpAdminCodePillComponent,
    SpAdminCopyableTextComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminStatCardComponent,
    SpAdminTableComponent,
    SpAdminTruncatedTextComponent,
  ],
  templateUrl: './admin-ai-usage.component.html',
  styles: [`
    .sp-admin-stat-grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 14px; }
    @media(min-width:900px){ .sp-admin-stat-grid { grid-template-columns: repeat(5, 1fr); } }
    .sp-admin-two-col { display: grid; gap: 24px; }
    @media(min-width:1100px){ .sp-admin-two-col { grid-template-columns: 1fr 1fr; align-items: start; } }
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

  recentTotalPages = computed(() => Math.max(1, Math.ceil(this.recentItems().length / this.recentPageSize)));
  pagedRecentItems = computed(() => {
    const page = Math.min(this.recentPage(), this.recentTotalPages());
    const start = (page - 1) * this.recentPageSize;
    return this.recentItems().slice(start, start + this.recentPageSize);
  });

  constructor(private svc: AiUsageService) {}

  ngOnInit(): void {
    this.svc.getSummary().subscribe({
      next: s => { this.summary.set(s); this.loadingSummary.set(false); },
      error: err => { this.summaryError.set(err.error?.error ?? 'Could not load summary.'); this.loadingSummary.set(false); },
    });
    this.svc.getRecent(100).subscribe({
      next: r => { this.recentItems.set(r.items); this.recentPage.set(1); this.loadingRecent.set(false); },
      error: err => { this.recentError.set(err.error?.error ?? 'Could not load recent calls.'); this.loadingRecent.set(false); },
    });
  }

  formatTime(iso: string): string {
    try { return new Date(iso).toLocaleTimeString('en-AU', { hour12: false }); } catch { return iso; }
  }

  featureLabel(key: string): string {
    return key.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase());
  }
}
