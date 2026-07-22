import { Component, OnInit, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AdminAiOperationsSummary } from '../../../core/models/admin.models';
import {
  SpAdminAlertComponent,
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminErrorStateComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminNotImplementedStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminStatusCardComponent,
  SpAdminStatusGridComponent,
  SpAdminTableComponent,
} from '../../../design-system/admin';
import type { SpAdminTableColumn } from '../../../design-system/admin';

@Component({
  selector: 'app-admin-ai-operations',
  standalone: true,
  templateUrl: './admin-ai-operations.component.html',
  imports: [
    CommonModule,
    RouterLink,
    SpAdminAlertComponent,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminErrorStateComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminNotImplementedStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminStatusCardComponent,
    SpAdminStatusGridComponent,
    SpAdminTableComponent,
  ],
})
export class AdminAiOperationsComponent implements OnInit {
  loading = signal(true);
  error = signal('');
  summary = signal<AdminAiOperationsSummary | null>(null);

  readonly providerUsageColumns: SpAdminTableColumn[] = [
    { key: 'provider', label: 'Provider' },
    { key: 'calls', label: 'Calls' },
    { key: 'successful', label: 'Successful' },
    { key: 'fallback', label: 'Fallback' },
    { key: 'costUsd', label: 'Cost (USD)' },
  ];

  readonly generationFailuresColumns: SpAdminTableColumn[] = [
    { key: 'timestampUtc', label: 'Time' },
    { key: 'pattern', label: 'Pattern' },
    { key: 'cefrLevel', label: 'CEFR' },
    { key: 'providerModel', label: 'Provider / model' },
    { key: 'attemptNumber', label: 'Attempt' },
  ];

  readonly recentFailuresColumns: SpAdminTableColumn[] = [
    { key: 'timestampUtc', label: 'Time' },
    { key: 'area', label: 'Area' },
    { key: 'providerModel', label: 'Provider / model' },
    { key: 'reason', label: 'Reason' },
    { key: 'status', label: 'Status' },
  ];

  readonly isEmpty = computed(() => {
    const s = this.summary();
    if (!s) return false;
    return s.providerUsage.totalCalls === 0
      && s.speakingEvaluationSummary.pendingCount === 0
      && s.speakingEvaluationSummary.completedCount === 0
      && s.speakingEvaluationSummary.failedCount === 0
      && s.writingEvaluationSummary.pendingCount === 0
      && s.writingEvaluationSummary.completedCount === 0
      && s.writingEvaluationSummary.failedCount === 0
      && s.generationQualitySummary.totalValidationFailures === 0
      && s.recentFailures.length === 0;
  });

  readonly statusTone = computed<'success' | 'warning' | 'danger'>(() => {
    switch (this.summary()?.overallStatus) {
      case 'Healthy': return 'success';
      case 'Degraded': return 'warning';
      case 'AttentionNeeded': return 'danger';
      default: return 'success';
    }
  });

  constructor(private adminApi: AdminApiService) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.adminApi.getAiOperationsSummary().subscribe({
      next: s => { this.summary.set(s); this.loading.set(false); },
      error: err => {
        this.error.set(err?.error?.error ?? err?.message ?? 'Failed to load AI operations summary.');
        this.loading.set(false);
      },
    });
  }

  gateTone(enabled: boolean): 'success' | 'danger' {
    // Enabled=true here always means "AI is allowed to do this" — for CEFR/objective/LP
    // gates that should stay dangerous even if enabled is the expected safe state (false).
    return enabled ? 'danger' : 'success';
  }

  signalTone(enabled: boolean): 'success' | 'neutral' {
    return enabled ? 'success' : 'neutral';
  }

  formatMinutes(minutes: number | null): string {
    if (minutes === null || minutes === undefined) return '—';
    if (minutes < 60) return `${Math.round(minutes)}m`;
    const hours = Math.floor(minutes / 60);
    const rem = Math.round(minutes % 60);
    return `${hours}h ${rem}m`;
  }
}
