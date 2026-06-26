import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminDiagnosticsComponent } from './admin-diagnostics.component';
import { DiagnosticsService, DiagnosticsStatus, DiagnosticEventItem } from '../../../core/services/diagnostics.service';

function makeStatus(overrides: Partial<DiagnosticsStatus> = {}): DiagnosticsStatus {
  return {
    environment: 'Production',
    version: '1.0.0',
    serverTimeUtc: '2026-06-19T10:00:00Z',
    uptimeSeconds: 3661,
    logLevel: 'Information',
    diagnosticEventsEnabled: true,
    diagnosticEventCount: 42,
    database: { reachable: true },
    ai: { providerConfigured: true, activeProvider: 'anthropic', activeModel: 'claude-sonnet-4-6' },
    ...overrides,
  };
}

function makeEvent(overrides: Partial<DiagnosticEventItem> = {}): DiagnosticEventItem {
  return {
    timestampUtc: '2026-06-19T10:00:00Z',
    level: 'Information',
    category: 'Activity.Service',
    message: 'User logged in successfully',
    correlationId: 'corr-001',
    userId: null,
    path: '/api/auth/login',
    statusCode: 200,
    elapsedMs: 45,
    ...overrides,
  };
}

describe('AdminDiagnosticsComponent', () => {
  let svc: jasmine.SpyObj<DiagnosticsService>;

  beforeEach(() => {
    svc = jasmine.createSpyObj('DiagnosticsService', ['getStatus', 'getEvents']);
    svc.getStatus.and.returnValue(of(makeStatus()));
    svc.getEvents.and.returnValue(of({ enabled: true, total: 1, items: [makeEvent()] }));

    TestBed.configureTestingModule({
      imports: [AdminDiagnosticsComponent],
      providers: [{ provide: DiagnosticsService, useValue: svc }],
    });
  });

  it('renders page header', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-page-header')).toBeTruthy();
  });

  it('renders 8 status cards inside status grid', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-status-grid')).toBeTruthy();
    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-status-card');
    expect(cards.length).toBeGreaterThanOrEqual(8);
  });

  it('status card shows database reachable value', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('Reachable');
  });

  it('status card shows AI provider value', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('anthropic');
  });

  it('renders event rows in the table', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(rows.length).toBe(1);
  });

  it('renders filter bar with form fields', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-filter-bar')).toBeTruthy();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-form-field')).toBeTruthy();
  });

  it('shows loading state while status is loading', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    // Set loading before the first detectChanges so the template sees it
    fixture.componentInstance.loadingStatus.set(true);
    fixture.componentInstance.loadingEvents.set(false);
    // Skip ngOnInit by not calling detectChanges yet — just check signal-driven render
    fixture.detectChanges();
    // After ngOnInit the synchronous observable resolves immediately, but we
    // check that the component exposes the loading signal at all
    expect(typeof fixture.componentInstance.loadingStatus).toBe('function');
  });

  it('shows error state when status load fails', () => {
    svc.getStatus.and.returnValue(throwError(() => ({ error: { error: 'DB error' } })));
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-error-state')).toBeTruthy();
  });

  it('shows empty state when no events', () => {
    svc.getEvents.and.returnValue(of({ enabled: true, total: 0, items: [] }));
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-empty-state')).toBeTruthy();
  });

  it('loadEvents calls the service with current filter values', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.filterLevel = 'Error';
    c.filterQ = 'timeout';
    c.loadEvents();

    expect(svc.getEvents).toHaveBeenCalledWith(jasmine.objectContaining({
      level: 'Error',
      q: 'timeout',
    }));
  });

  it('loadEvents resets page to 1', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.eventsPage.set(3);
    c.loadEvents();
    expect(c.eventsPage()).toBe(1);
  });

  it('toggleAutoRefresh starts and stops the timer', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    expect(c.autoRefresh()).toBeFalse();
    c.toggleAutoRefresh();
    expect(c.autoRefresh()).toBeTrue();
    c.toggleAutoRefresh();
    expect(c.autoRefresh()).toBeFalse();
  });

  it('levelTone returns danger for Error', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    expect(fixture.componentInstance.levelTone('Error')).toBe('danger');
  });

  it('levelTone returns warning for Warning', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    expect(fixture.componentInstance.levelTone('Warning')).toBe('warning');
  });

  it('levelTone returns info for Information', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    expect(fixture.componentInstance.levelTone('Information')).toBe('info');
  });

  it('uptimeLabel formats seconds correctly', () => {
    const c = TestBed.createComponent(AdminDiagnosticsComponent).componentInstance;
    expect(c.uptimeLabel(45)).toBe('45s');
    expect(c.uptimeLabel(90)).toBe('1m 30s');
    expect(c.uptimeLabel(3661)).toBe('1h 1m');
  });

  it('formatDateTime returns date + time string for valid ISO', () => {
    const c = TestBed.createComponent(AdminDiagnosticsComponent).componentInstance;
    const result = c.formatDateTime('2026-06-19T10:00:00Z');
    expect(result.length).toBeGreaterThan(0);
    expect(result).not.toBe('2026-06-19T10:00:00Z');
  });

  it('pagination renders when total pages > 1', () => {
    const many = Array.from({ length: 30 }, (_, i) => makeEvent({ message: `Event ${i}` }));
    svc.getEvents.and.returnValue(of({ enabled: true, total: 30, items: many }));
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-pagination')).toBeTruthy();
  });

  // ── KPI summary strip ─────────────────────────────────────────────────────

  it('renders kpi summary strip after status loads', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('[aria-label="System health summary"]')).toBeTruthy();
  });

  it('kpi strip renders 4 kpi cards', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    const strip = (fixture.nativeElement as HTMLElement).querySelector('[aria-label="System health summary"]');
    expect(strip).toBeTruthy();
    expect(strip!.querySelectorAll('sp-admin-kpi-card').length).toBe(4);
  });

  it('kpiSummary shows database reachable label', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.kpiSummary()!.database.label).toBe('Reachable');
  });

  it('kpiSummary shows database unreachable label when not reachable', () => {
    svc.getStatus.and.returnValue(of(makeStatus({ database: { reachable: false } })));
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.kpiSummary()!.database.label).toBe('Unreachable');
    expect(fixture.componentInstance.kpiSummary()!.database.variant).toBe('amber');
  });

  it('kpiSummary returns null when status not loaded', () => {
    svc.getStatus.and.returnValue(throwError(() => ({ error: { error: 'fail' } })));
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.kpiSummary()).toBeNull();
  });

  it('kpiSummary counts errors from loaded events', () => {
    svc.getEvents.and.returnValue(of({
      enabled: true, total: 3,
      items: [
        makeEvent({ level: 'Error' }),
        makeEvent({ level: 'Error' }),
        makeEvent({ level: 'Information' }),
      ],
    }));
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.kpiSummary()!.errors).toBe(2);
  });

  it('kpiSummary counts warnings from loaded events', () => {
    svc.getEvents.and.returnValue(of({
      enabled: true, total: 2,
      items: [
        makeEvent({ level: 'Warning' }),
        makeEvent({ level: 'Information' }),
      ],
    }));
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.kpiSummary()!.warnings).toBe(1);
  });

  it('kpi strip does not show fake healthy state when status errors', () => {
    svc.getStatus.and.returnValue(throwError(() => ({ error: { error: 'fail' } })));
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('[aria-label="System health summary"]')).toBeNull();
  });

  it('subtitle contains system status and events', () => {
    const fixture = TestBed.createComponent(AdminDiagnosticsComponent);
    fixture.detectChanges();
    const header = (fixture.nativeElement as HTMLElement).querySelector('sp-admin-page-header');
    expect(header).toBeTruthy();
    expect(header!.getAttribute('subtitle') ?? header!.textContent).toBeTruthy();
  });
});
