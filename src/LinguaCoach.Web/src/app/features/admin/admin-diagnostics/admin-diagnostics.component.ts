import { Component, OnInit, OnDestroy, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DiagnosticsService, DiagnosticsStatus, DiagnosticEventItem } from '../../../core/services/diagnostics.service';
import { GenerationQualityService, GenerationQualitySummary } from '../../../core/services/generation-quality.service';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { DataIntegritySweepResult } from '../../../core/models/admin.models';
import { SpAdminEventFeedComponent, EventFeedItem } from '../../../design-system/admin/components/event-feed/sp-admin-event-feed.component';
import { SpAdminBreakdownBarsComponent, BreakdownBarItem } from '../../../design-system/admin/components/breakdown-bars/sp-admin-breakdown-bars.component';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCodePillComponent,
  SpAdminCopyableTextComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminInputComponent,
  SpAdminKpiCardComponent,
  SpAdminLoadingStateComponent,
  SpAdminNotImplementedStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminStatusCardComponent,
  SpAdminStatusGridComponent,
  SpAdminTableComponent,
  SpAdminTruncatedTextComponent,
} from '../../../design-system/admin';
import type { SpAdminTableColumn, SpAdminTableFilter } from '../../../design-system/admin';
import { eventLevelLabel } from '../../../design-system/admin/utils/admin-badge.utils';

@Component({
  selector: 'app-admin-diagnostics',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCodePillComponent,
    SpAdminCopyableTextComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminInputComponent,
    SpAdminKpiCardComponent,
    SpAdminLoadingStateComponent,
    SpAdminNotImplementedStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminStatusCardComponent,
    SpAdminStatusGridComponent,
    SpAdminTableComponent,
    SpAdminTruncatedTextComponent,
    SpAdminEventFeedComponent,
    SpAdminBreakdownBarsComponent,
  ],
  templateUrl: './admin-diagnostics.component.html',
})
export class AdminDiagnosticsComponent implements OnInit, OnDestroy {
  readonly dataIntegrityColumns: SpAdminTableColumn[] = [
    { key: 'category', label: 'Category' },
    { key: 'description', label: 'Description' },
    { key: 'totalChecked', label: 'Checked' },
    { key: 'issuesFound', label: 'Issues' },
    { key: 'status', label: 'Status' },
  ];

  readonly eventColumns: SpAdminTableColumn[] = [
    { key: 'timestampUtc', label: 'Time' },
    { key: 'level', label: 'Level' },
    { key: 'category', label: 'Category' },
    { key: 'message', label: 'Message' },
    { key: 'correlationId', label: 'Correlation' },
  ];

  readonly validationFailureColumns: SpAdminTableColumn[] = [
    { key: 'timestampUtc', label: 'Time' },
    { key: 'pattern', label: 'Pattern' },
    { key: 'activityTypeName', label: 'Type' },
    { key: 'cefrLevel', label: 'CEFR' },
    { key: 'attemptNumber', label: 'Attempt' },
    { key: 'providerModel', label: 'Provider / Model' },
    { key: 'validationErrors', label: 'Errors' },
  ];

  readonly providerBreakdownColumns: SpAdminTableColumn[] = [
    { key: 'providerName', label: 'Provider' },
    { key: 'modelName', label: 'Model' },
    { key: 'totalFailures', label: 'Failures' },
    { key: 'abandonedCount', label: 'Abandoned' },
  ];

  readonly patternBreakdownColumns: SpAdminTableColumn[] = [
    { key: 'patternKey', label: 'Pattern' },
    { key: 'totalFailures', label: 'Failures' },
    { key: 'abandonedCount', label: 'Abandoned' },
  ];

  readonly cefrBreakdownColumns: SpAdminTableColumn[] = [
    { key: 'cefrLevel', label: 'CEFR' },
    { key: 'totalFailures', label: 'Failures' },
  ];

  readonly promptSummaryColumns: SpAdminTableColumn[] = [
    { key: 'key', label: 'Prompt key' },
    { key: 'version', label: 'Version' },
    { key: 'contentHashShort', label: 'Hash' },
    { key: 'maxInputTokens', label: 'Max input tokens' },
    { key: 'maxOutputTokens', label: 'Max output tokens' },
    { key: 'seededAtUtc', label: 'Seeded at' },
  ];

  readonly eventFilters = computed<SpAdminTableFilter[]>(() => [
    { key: 'level', label: 'Level', options: this.levelOptions, value: this.filterLevel() },
    { key: 'limit', label: 'Limit', options: this.limitOptions, value: this.filterLimit().toString() },
  ]);

  onEventFilterChange(event: { key: string; value: string }): void {
    if (event.key === 'level') this.filterLevel.set(event.value);
    else if (event.key === 'limit') this.filterLimit.set(+event.value);
    this.loadEvents();
  }

  status = signal<DiagnosticsStatus | null>(null);
  events = signal<DiagnosticEventItem[]>([]);
  total = signal(0);
  loadingStatus = signal(true);
  loadingEvents = signal(false);
  statusError = signal('');
  eventsError = signal('');

  filterLevel = signal('');
  filterCategory = '';
  filterCorrelationId = '';
  filterQ = '';
  filterLimit = signal(100);
  eventsPage = signal(1);
  readonly eventsPageSize = 25;

  eventsTotalPages = computed(() => Math.max(1, Math.ceil(this.events().length / this.eventsPageSize)));

  readonly recentFeedItems = computed<EventFeedItem[]>(() =>
    this.events().slice(0, 8).map((e, i) => ({
      id: e.correlationId ?? String(i),
      timestamp: e.timestampUtc,
      title: e.message?.slice(0, 80) ?? 'Event',
      level: e.level as EventFeedItem['level'],
      category: e.category ?? undefined,
      correlationId: e.correlationId ?? undefined,
    }))
  );

  readonly severityBreakdownItems = computed<BreakdownBarItem[]>(() => {
    const evs = this.events();
    if (evs.length === 0) return [];
    const total = evs.length;
    const errors      = evs.filter(e => e.level?.toLowerCase() === 'error').length;
    const warnings    = evs.filter(e => e.level?.toLowerCase() === 'warning').length;
    const information = evs.filter(e => e.level?.toLowerCase() === 'information').length;
    const debug       = evs.filter(e => e.level?.toLowerCase() === 'debug').length;
    const rows: BreakdownBarItem[] = [
      { label: 'Error',       value: errors,      pct: Math.round((errors      / total) * 100), tone: 'danger' },
      { label: 'Warning',     value: warnings,    pct: Math.round((warnings    / total) * 100), tone: 'amber' },
      { label: 'Information', value: information, pct: Math.round((information / total) * 100), tone: 'indigo' },
      { label: 'Debug',       value: debug,       pct: Math.round((debug       / total) * 100), tone: 'slate' },
    ];
    return rows.filter(r => r.value > 0);
  });

  readonly kpiSummary = computed(() => {
    const s = this.status();
    const evs = this.events();
    if (!s) return null;
    const errors = evs.filter(e => e.level?.toLowerCase() === 'error').length;
    const warnings = evs.filter(e => e.level?.toLowerCase() === 'warning').length;
    return {
      database: { label: s.database.reachable ? 'Reachable' : 'Unreachable', variant: (s.database.reachable ? 'green' : 'amber') as 'green' | 'amber' },
      ai: { label: s.ai.providerConfigured ? (s.ai.activeProvider ?? 'Configured') : 'Not configured', variant: (s.ai.providerConfigured ? 'indigo' : 'amber') as 'indigo' | 'amber' },
      errors,
      warnings,
    };
  });

  pagedEvents = computed(() => {
    const page = Math.min(this.eventsPage(), this.eventsTotalPages());
    const start = (page - 1) * this.eventsPageSize;
    return this.events().slice(start, start + this.eventsPageSize);
  });

  autoRefresh = signal(false);
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  readonly levelOptions = [
    { value: '', label: 'All levels' },
    { value: 'Information', label: 'Information' },
    { value: 'Warning', label: 'Warning' },
    { value: 'Error', label: 'Error' },
    { value: 'Debug', label: 'Debug' },
  ];
  readonly limitOptions = [
    { value: '50', label: '50' },
    { value: '100', label: '100' },
    { value: '250', label: '250' },
    { value: '500', label: '500' },
  ];

  // ── Generation quality ─────────────────────────────────────────────────────
  generationQuality = signal<GenerationQualitySummary | null>(null);
  loadingQuality = signal(false);
  qualityError = signal('');

  readonly qFailureSummary = computed(() => this.generationQuality()?.validationFailureSummary ?? null);
  readonly qLatestFailures = computed(() => this.generationQuality()?.latestFailures ?? []);
  readonly qPatternBreakdown = computed(() => this.generationQuality()?.patternFailureBreakdown ?? []);
  readonly qCefrBreakdown = computed(() => this.generationQuality()?.cefrFailureBreakdown ?? []);
  readonly qPromptSummary = computed(() => this.generationQuality()?.promptSummary ?? []);
  readonly qProviderBreakdown = computed(() => this.generationQuality()?.providerBreakdown ?? []);
  readonly qAbandonedWarning = computed(() => this.generationQuality()?.abandonedWarning ?? null);
  readonly qRetentionDays = computed(() => this.generationQuality()?.retentionDays ?? 90);

  // ── Data integrity sweep (Sprint 11) — FK/orphan checks + aggregated per-entity issue counts,
  // distinct from the AI-generation health data above. ──────────────────────
  dataIntegrity = signal<DataIntegritySweepResult | null>(null);
  loadingDataIntegrity = signal(false);
  dataIntegrityError = signal('');

  constructor(
    private svc: DiagnosticsService,
    private qualitySvc: GenerationQualityService,
    private adminApi: AdminApiService,
  ) {}

  ngOnInit(): void {
    this.loadStatus();
    this.loadEvents();
    this.loadGenerationQuality();
    this.loadDataIntegrity();
  }

  loadDataIntegrity(): void {
    this.loadingDataIntegrity.set(true);
    this.dataIntegrityError.set('');
    this.adminApi.getDataIntegritySweep().subscribe({
      next: r => { this.dataIntegrity.set(r); this.loadingDataIntegrity.set(false); },
      error: err => { this.loadingDataIntegrity.set(false); this.dataIntegrityError.set(err.error?.error ?? 'Could not load data integrity sweep.'); },
    });
  }

  ngOnDestroy(): void {
    this.stopAutoRefresh();
  }

  loadStatus(): void {
    this.loadingStatus.set(true);
    this.statusError.set('');
    this.svc.getStatus().subscribe({
      next: s => { this.status.set(s); this.loadingStatus.set(false); },
      error: err => { this.loadingStatus.set(false); this.statusError.set(err.error?.error ?? 'Could not load diagnostics status.'); },
    });
  }

  private eventSearchDebounce?: ReturnType<typeof setTimeout>;
  onEventSearchChange(value: string): void {
    this.filterQ = value;
    clearTimeout(this.eventSearchDebounce);
    this.eventSearchDebounce = setTimeout(() => this.loadEvents(), 300);
  }

  loadEvents(): void {
    this.loadingEvents.set(true);
    this.eventsError.set('');
    this.svc.getEvents({
      level: this.filterLevel() || undefined,
      category: this.filterCategory || undefined,
      correlationId: this.filterCorrelationId || undefined,
      q: this.filterQ || undefined,
      limit: this.filterLimit(),
    }).subscribe({
      next: r => { this.events.set(r.items); this.total.set(r.total); this.eventsPage.set(1); this.loadingEvents.set(false); },
      error: err => { this.loadingEvents.set(false); this.eventsError.set(err.error?.error ?? 'Could not load events.'); },
    });
  }

  toggleAutoRefresh(): void {
    if (this.autoRefresh()) {
      this.stopAutoRefresh();
      this.autoRefresh.set(false);
    } else {
      this.autoRefresh.set(true);
      this.refreshTimer = setInterval(() => this.loadEvents(), 5000);
    }
  }

  private stopAutoRefresh(): void {
    if (this.refreshTimer !== null) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = null;
    }
  }

  loadGenerationQuality(): void {
    this.loadingQuality.set(true);
    this.qualityError.set('');
    this.qualitySvc.getSummary(30).subscribe({
      next: q => { this.generationQuality.set(q); this.loadingQuality.set(false); },
      error: err => { this.loadingQuality.set(false); this.qualityError.set(err.error?.error ?? 'Could not load generation quality data.'); },
    });
  }

  readonly eventLevelLabel = eventLevelLabel;

  levelTone(level: string): 'success' | 'warning' | 'danger' | 'neutral' | 'info' {
    switch (level?.toLowerCase()) {
      case 'error':       return 'danger';
      case 'warning':     return 'warning';
      case 'information': return 'info';
      case 'debug':       return 'neutral';
      default:            return 'neutral';
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

  uptimeLabel(seconds: number): string {
    if (seconds < 60) return `${seconds}s`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}m ${seconds % 60}s`;
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    return `${h}h ${m}m`;
  }
}
