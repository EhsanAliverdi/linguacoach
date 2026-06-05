import { Component, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AiUsageService, AiUsageSummary, AiUsageRecentItem } from '../../../core/services/ai-usage.service';

@Component({
  selector: 'app-admin-ai-usage',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './admin-ai-usage.component.html',
})
export class AdminAiUsageComponent implements OnInit {
  summary = signal<AiUsageSummary | null>(null);
  recentItems = signal<AiUsageRecentItem[]>([]);
  loadingSummary = signal(true);
  loadingRecent = signal(true);
  summaryError = signal('');
  recentError = signal('');

  constructor(private svc: AiUsageService) {}

  ngOnInit(): void {
    this.svc.getSummary().subscribe({
      next: s => { this.summary.set(s); this.loadingSummary.set(false); },
      error: err => { this.summaryError.set(err.error?.error ?? 'Could not load summary.'); this.loadingSummary.set(false); },
    });
    this.svc.getRecent(100).subscribe({
      next: r => { this.recentItems.set(r.items); this.loadingRecent.set(false); },
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
