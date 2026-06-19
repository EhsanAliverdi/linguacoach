import { Component, OnInit, OnDestroy, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DiagnosticsService, DiagnosticsStatus, DiagnosticEventItem } from '../../../core/services/diagnostics.service';
import {
  SpAdminBadgeComponent,
  SpAdminButtonComponent,
  SpAdminCardComponent,
  SpAdminCopyableTextComponent,
  SpAdminEmptyStateComponent,
  SpAdminErrorStateComponent,
  SpAdminFilterBarComponent,
  SpAdminFormFieldComponent,
  SpAdminInputComponent,
  SpAdminLoadingStateComponent,
  SpAdminPageBodyComponent,
  SpAdminPageHeaderComponent,
  SpAdminPaginationComponent,
  SpAdminSelectComponent,
  SpAdminStatCardComponent,
  SpAdminTableComponent,
  SpAdminTruncatedTextComponent,
} from '../../../admin';
import { eventLevelLabel } from '../../../admin/utils/admin-badge.utils';

@Component({
  selector: 'app-admin-diagnostics',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SpAdminBadgeComponent,
    SpAdminButtonComponent,
    SpAdminCardComponent,
    SpAdminCopyableTextComponent,
    SpAdminEmptyStateComponent,
    SpAdminErrorStateComponent,
    SpAdminFilterBarComponent,
    SpAdminFormFieldComponent,
    SpAdminInputComponent,
    SpAdminLoadingStateComponent,
    SpAdminPageBodyComponent,
    SpAdminPageHeaderComponent,
    SpAdminPaginationComponent,
    SpAdminSelectComponent,
    SpAdminStatCardComponent,
    SpAdminTableComponent,
    SpAdminTruncatedTextComponent,
  ],
  templateUrl: './admin-diagnostics.component.html',
  styles: [`
    .sp-admin-diagnostics-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(190px,1fr));gap:12px;}
    .sp-admin-diagnostics-actions{display:flex;align-items:center;gap:8px;flex-wrap:wrap;}
    .sp-admin-diagnostics-count{padding:10px 16px;border-top:1px solid #F1F5F9;color:#64748B;font-size:12px;font-weight:600;}
    .sp-admin-diagnostics-truncate{max-width:180px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
    .sp-admin-diagnostics-message{max-width:480px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
  `],
})
export class AdminDiagnosticsComponent implements OnInit, OnDestroy {
  status = signal<DiagnosticsStatus | null>(null);
  events = signal<DiagnosticEventItem[]>([]);
  total = signal(0);
  loadingStatus = signal(true);
  loadingEvents = signal(false);
  statusError = signal('');
  eventsError = signal('');

  // Filters
  filterLevel = '';
  filterCategory = '';
  filterCorrelationId = '';
  filterQ = '';
  filterLimit = 100;
  eventsPage = signal(1);
  readonly eventsPageSize = 25;

  eventsTotalPages = computed(() => Math.max(1, Math.ceil(this.events().length / this.eventsPageSize)));
  pagedEvents = computed(() => {
    const page = Math.min(this.eventsPage(), this.eventsTotalPages());
    const start = (page - 1) * this.eventsPageSize;
    return this.events().slice(start, start + this.eventsPageSize);
  });

  autoRefresh = signal(false);
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  readonly levels = ['', 'Information', 'Warning', 'Error', 'Debug'];
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

  constructor(private svc: DiagnosticsService) {}

  ngOnInit(): void {
    this.loadStatus();
    this.loadEvents();
  }

  ngOnDestroy(): void {
    this.stopAutoRefresh();
  }

  loadStatus(): void {
    this.loadingStatus.set(true);
    this.statusError.set('');
    this.svc.getStatus().subscribe({
      next: s => { this.status.set(s); this.loadingStatus.set(false); },
      error: err => {
        this.loadingStatus.set(false);
        this.statusError.set(err.error?.error ?? 'Could not load diagnostics status.');
      },
    });
  }

  loadEvents(): void {
    this.loadingEvents.set(true);
    this.eventsError.set('');
    this.svc.getEvents({
      level: this.filterLevel || undefined,
      category: this.filterCategory || undefined,
      correlationId: this.filterCorrelationId || undefined,
      q: this.filterQ || undefined,
      limit: this.filterLimit,
    }).subscribe({
      next: r => {
        this.events.set(r.items);
        this.total.set(r.total);
        this.eventsPage.set(1);
        this.loadingEvents.set(false);
      },
      error: err => {
        this.loadingEvents.set(false);
        this.eventsError.set(err.error?.error ?? 'Could not load events.');
      },
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

  levelColour(level: string): string {
    switch (level?.toLowerCase()) {
      case 'error': return 'var(--sp-speaking)';
      case 'warning': return 'var(--sp-warn)';
      case 'information': return 'var(--sp-writing)';
      default: return 'var(--sp-muted)';
    }
  }

  levelBg(level: string): string {
    switch (level?.toLowerCase()) {
      case 'error': return '#FEE2E2';
      case 'warning': return 'var(--sp-warn-soft)';
      case 'information': return 'var(--sp-writing-soft)';
      default: return 'var(--sp-canvas2)';
    }
  }

  readonly eventLevelLabel = eventLevelLabel;

  levelTone(level: string): 'success' | 'warning' | 'danger' | 'neutral' | 'info' {
    switch (level?.toLowerCase()) {
      case 'error': return 'danger';
      case 'warning': return 'warning';
      case 'information': return 'info';
      case 'debug': return 'neutral';
      default: return 'neutral';
    }
  }

  formatTime(iso: string): string {
    try {
      return new Date(iso).toLocaleTimeString('en-AU', { hour12: false });
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
