import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DiagnosticsService, DiagnosticsStatus, DiagnosticEventItem } from '../../../core/services/diagnostics.service';
import { SpAdminErrorStateComponent, SpAdminLoadingStateComponent, SpAdminPageHeaderComponent } from '../../../admin';

@Component({
  selector: 'app-admin-diagnostics',
  standalone: true,
  imports: [CommonModule, FormsModule, SpAdminErrorStateComponent, SpAdminLoadingStateComponent, SpAdminPageHeaderComponent],
  templateUrl: './admin-diagnostics.component.html',
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

  autoRefresh = signal(false);
  private refreshTimer: ReturnType<typeof setInterval> | null = null;

  readonly levels = ['', 'Information', 'Warning', 'Error', 'Debug'];

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
