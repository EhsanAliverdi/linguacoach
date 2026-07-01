import { TestBed } from '@angular/core/testing';
import { of, throwError, Subject } from 'rxjs';
import { AdminAiUsageComponent, PeriodPreset } from './admin-ai-usage.component';
import { AiUsageService, AiUsageSummary, AiUsageRecentItem, AiUsageTrendBucket } from '../../../core/services/ai-usage.service';
import { AdminApiService } from '../../../core/services/admin.api.service';

function makeSummary(overrides: Partial<AiUsageSummary> = {}): AiUsageSummary {
  return {
    totalCalls: 10,
    successfulCalls: 9,
    failedCalls: 1,
    fallbackCalls: 2,
    totalCostUsd: 0.0123,
    totalInputTokens: 4000,
    totalOutputTokens: 1500,
    totalTokens: 5500,
    successRate: 90,
    zeroCostCallCount: 0,
    zeroCostTotalTokens: 0,
    byProvider: [{ provider: 'anthropic', calls: 10, successful: 9, fallback: 2, costUsd: 0.0123 }],
    byFeature: [{ feature: 'lesson_generation', calls: 10, successful: 9, costUsd: 0.0123 }],
    ...overrides,
  };
}

function makeRecentItem(overrides: Partial<AiUsageRecentItem> = {}): AiUsageRecentItem {
  return {
    id: 'abc123',
    createdAt: '2026-06-19T10:00:00Z',
    studentProfileId: null,
    featureKey: 'lesson_generation',
    provider: 'anthropic',
    model: 'claude-sonnet-4-6',
    isFallback: false,
    wasSuccessful: true,
    failureReason: null,
    inputTokens: 100,
    outputTokens: 200,
    costUsd: 0.0012,
    durationMs: 850,
    correlationId: 'corr-001',
    ...overrides,
  };
}

describe('AdminAiUsageComponent', () => {
  let svc: jasmine.SpyObj<AiUsageService>;
  let adminApi: jasmine.SpyObj<AdminApiService>;

  beforeEach(() => {
    svc = jasmine.createSpyObj('AiUsageService', ['getSummary', 'getRecent', 'exportUsageCsv', 'getTrends']);
    svc.getSummary.and.returnValue(of(makeSummary()));
    svc.getRecent.and.returnValue(of({ items: [makeRecentItem()], totalCount: 1, page: 1, pageSize: 25, totalPages: 1 }));
    svc.exportUsageCsv.and.returnValue(of(new Blob(['header\nrow1'], { type: 'text/csv' })));
    svc.getTrends.and.returnValue(of([] as AiUsageTrendBucket[]));

    adminApi = jasmine.createSpyObj('AdminApiService', ['listStudents', 'getAiUsageTrends', 'getAiUsageCategoryBreakdown']);
    adminApi.listStudents.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 50, totalPages: 1 }));
    adminApi.getAiUsageTrends.and.returnValue(of({ period: '30d', buckets: [] }));
    adminApi.getAiUsageCategoryBreakdown.and.returnValue(of({ period: '30d', categories: [] }));

    TestBed.configureTestingModule({
      imports: [AdminAiUsageComponent],
      providers: [
        { provide: AiUsageService, useValue: svc },
        { provide: AdminApiService, useValue: adminApi },
      ],
    });
  });

  it('renders page header', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-page-header')).toBeTruthy();
  });

  it('renders kpi cards after summary loads', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-kpi-card');
    expect(cards.length).toBeGreaterThanOrEqual(4);
  });

  it('renders total requests kpi card', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const cards = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-kpi-card'));
    const labels = cards.map(c => c.getAttribute('label') ?? c.getAttribute('ng-reflect-label') ?? c.textContent ?? '');
    expect(labels.some(l => l.toLowerCase().includes('request'))).toBeTrue();
  });

  it('renders total cost kpi card', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const cards = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-kpi-card'));
    const labels = cards.map(c => c.getAttribute('label') ?? c.getAttribute('ng-reflect-label') ?? c.textContent ?? '');
    expect(labels.some(l => l.toLowerCase().includes('cost'))).toBeTrue();
  });

  it('renders failed calls kpi card', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const cards = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-kpi-card'));
    const labels = cards.map(c => c.getAttribute('label') ?? c.getAttribute('ng-reflect-label') ?? c.textContent ?? '');
    expect(labels.some(l => l.toLowerCase().includes('failed'))).toBeTrue();
  });

  it('renders provider and feature summary tables', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const tables = (fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-table');
    expect(tables.length).toBeGreaterThanOrEqual(2);
  });

  it('renders recent calls table with rows', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(rows.length).toBeGreaterThan(0);
  });

  it('renders filter bar for recent calls', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-filter-bar')).toBeTruthy();
  });

  it('onRecentProviderChange triggers getRecent with provider filter', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getRecent.calls.reset();

    c.onRecentProviderChange('openai');

    expect(svc.getRecent).toHaveBeenCalledTimes(1);
    const args = svc.getRecent.calls.mostRecent().args;
    expect((args[3] as { provider?: string })?.provider).toBe('openai');
  });

  it('onRecentStatusChange triggers getRecent with status filter', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getRecent.calls.reset();

    c.onRecentStatusChange('failed');

    expect(svc.getRecent).toHaveBeenCalledTimes(1);
    const args = svc.getRecent.calls.mostRecent().args;
    expect((args[3] as { status?: string })?.status).toBe('failed');
  });

  it('onRecentStatusChange to fallback triggers getRecent with fallback status', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getRecent.calls.reset();

    c.onRecentStatusChange('fallback');

    const args = svc.getRecent.calls.mostRecent().args;
    expect((args[3] as { status?: string })?.status).toBe('fallback');
  });

  it('resets page to 1 when provider filter changes', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.recentPage.set(2);
    c.onRecentProviderChange('anthropic');
    expect(c.recentPage()).toBe(1);
  });

  it('shows error state when summary fails', () => {
    svc.getSummary.and.returnValue(throwError(() => ({ error: { error: 'Server error' } })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-error-state')).toBeTruthy();
  });

  it('shows empty state when no recent items', () => {
    svc.getRecent.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 1 }));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-empty-state')).toBeTruthy();
  });

  it('featureLabel formats underscore keys to title case', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    expect(fixture.componentInstance.featureLabel('lesson_generation')).toBe('Lesson Generation');
  });

  it('formatDateTime returns a non-empty string for valid ISO', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const result = fixture.componentInstance.formatDateTime('2026-06-19T10:00:00Z');
    expect(result.length).toBeGreaterThan(0);
    expect(result).not.toBe('2026-06-19T10:00:00Z');
  });

  it('providerOptions derives unique sorted providers from recent items', () => {
    svc.getRecent.and.returnValue(of({ items: [
      makeRecentItem({ id: '1', provider: 'openai' }),
      makeRecentItem({ id: '2', provider: 'anthropic' }),
      makeRecentItem({ id: '3', provider: 'openai' }),
    ], totalCount: 3, page: 1, pageSize: 25, totalPages: 1 }));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    const opts = c.providerOptions();
    expect(opts.length).toBe(2);
    expect(opts[0].value).toBe('anthropic');
    expect(opts[1].value).toBe('openai');
  });

  // ── period preset tests ─────────────────────────────────────────────────────

  it('default load calls getSummary without date params', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    // 'all' preset → no range → service called with undefined range and empty column filter
    const args = svc.getSummary.calls.mostRecent().args;
    expect(args[0]).toBeUndefined();
  });

  it('default load calls getRecent without date params', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(svc.getRecent).toHaveBeenCalledWith(1, 25, undefined, jasmine.anything());
  });

  it('onPeriodChange to last7days passes a from date to getSummary', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getSummary.calls.reset();
    fixture.componentInstance.onPeriodChange('7d');
    const range = svc.getSummary.calls.mostRecent().args[0] as { from?: string } | undefined;
    expect(range).toBeTruthy();
    expect(range!.from).toBeDefined();
    const from = new Date(range!.from!);
    expect(from.getTime()).toBeLessThan(Date.now());
  });

  it('onPeriodChange to last30days passes a from date approximately 30 days ago', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getSummary.calls.reset();
    const before = Date.now();
    fixture.componentInstance.onPeriodChange('30d');
    const range = svc.getSummary.calls.mostRecent().args[0] as { from?: string } | undefined;
    expect(range?.from).toBeDefined();
    const fromMs = new Date(range!.from!).getTime();
    const expectedMs = before - 30 * 24 * 60 * 60 * 1000;
    expect(Math.abs(fromMs - expectedMs)).toBeLessThan(5000);
  });

  it('onPeriodChange to today passes UTC start of today', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getSummary.calls.reset();
    fixture.componentInstance.onPeriodChange('today');
    const range = svc.getSummary.calls.mostRecent().args[0] as { from?: string } | undefined;
    expect(range?.from).toBeDefined();
    const from = new Date(range!.from!);
    const now = new Date();
    expect(from.getUTCFullYear()).toBe(now.getUTCFullYear());
    expect(from.getUTCMonth()).toBe(now.getUTCMonth());
    expect(from.getUTCDate()).toBe(now.getUTCDate());
    expect(from.getUTCHours()).toBe(0);
    expect(from.getUTCMinutes()).toBe(0);
  });

  it('onPeriodChange to month passes UTC start of current month', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getSummary.calls.reset();
    fixture.componentInstance.onPeriodChange('month');
    const range = svc.getSummary.calls.mostRecent().args[0] as { from?: string } | undefined;
    expect(range?.from).toBeDefined();
    const from = new Date(range!.from!);
    const now = new Date();
    expect(from.getUTCDate()).toBe(1);
    expect(from.getUTCMonth()).toBe(now.getUTCMonth());
  });

  it('onPeriodChange resets recentPage to 1', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentPage.set(3);
    c.onPeriodChange('7d');
    expect(c.recentPage()).toBe(1);
  });

  it('buildRange returns undefined for all preset', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    expect(fixture.componentInstance.buildRange('all')).toBeUndefined();
  });

  it('buildRange returns object with from for 7d preset', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const range = fixture.componentInstance.buildRange('7d');
    expect(range).toBeDefined();
    expect(range!.from).toBeDefined();
    expect(range!.to).toBeUndefined();
  });

  it('renders period preset select in filter bar', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-filter-bar')).toBeTruthy();
    expect((fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-select').length).toBeGreaterThanOrEqual(1);
  });

  it('summary cards reflect filtered response data', () => {
    svc.getSummary.and.returnValue(of(makeSummary({ totalCalls: 3, totalCostUsd: 0.009 })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.summary()!.totalCalls).toBe(3);
    expect(fixture.componentInstance.summary()!.totalCostUsd).toBe(0.009);
  });

  // ── pagination tests ────────────────────────────────────────────────────────

  it('getRecent is called with page=1 and pageSize=25 on init', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(svc.getRecent).toHaveBeenCalledWith(1, 25, undefined, jasmine.anything());
  });

  it('recentTotalPages signal is set from server response', () => {
    svc.getRecent.and.returnValue(of({ items: [makeRecentItem()], totalCount: 100, page: 1, pageSize: 25, totalPages: 4 }));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.recentTotalPages()).toBe(4);
  });

  it('recentTotalCount signal is set from server response', () => {
    svc.getRecent.and.returnValue(of({ items: [makeRecentItem()], totalCount: 87, page: 1, pageSize: 25, totalPages: 4 }));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.recentTotalCount()).toBe(87);
  });

  it('pagination component renders when totalPages > 1', () => {
    svc.getRecent.and.returnValue(of({ items: [makeRecentItem()], totalCount: 100, page: 1, pageSize: 25, totalPages: 4 }));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-pagination')).toBeTruthy();
  });

  it('pagination component does not render when totalPages = 1', () => {
    svc.getRecent.and.returnValue(of({ items: [makeRecentItem()], totalCount: 5, page: 1, pageSize: 25, totalPages: 1 }));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-pagination')).toBeNull();
  });

  it('onRecentPageChange calls getRecent with updated page and same range', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getRecent.calls.reset();
    fixture.componentInstance.onRecentPageChange(3);
    expect(fixture.componentInstance.recentPage()).toBe(3);
    expect(svc.getRecent).toHaveBeenCalledWith(3, 25, undefined, jasmine.anything());
  });

  it('onPeriodChange resets page to 1 then calls getRecent with page=1', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentPage.set(3);
    svc.getRecent.calls.reset();
    c.onPeriodChange('7d');
    expect(c.recentPage()).toBe(1);
    const firstCall = svc.getRecent.calls.first();
    expect(firstCall.args[0]).toBe(1);
  });

  it('recent items signal is populated from server response items array', () => {
    svc.getRecent.and.returnValue(of({ items: [
      makeRecentItem({ id: 'x1' }),
      makeRecentItem({ id: 'x2' }),
    ], totalCount: 2, page: 1, pageSize: 25, totalPages: 1 }));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.recentItems().length).toBe(2);
    expect(fixture.componentInstance.recentItems()[0].id).toBe('x1');
    expect(fixture.componentInstance.recentItems()[1].id).toBe('x2');
  });

  // ── server-side filter tests ────────────────────────────────────────────────

  it('onRecentProviderChange sets filter signal, resets page to 1, calls getRecent', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentPage.set(3);
    svc.getRecent.calls.reset();

    c.onRecentProviderChange('anthropic');

    expect(c.recentProviderFilter()).toBe('anthropic');
    expect(c.recentPage()).toBe(1);
    expect(svc.getRecent).toHaveBeenCalledTimes(1);
    const args = svc.getRecent.calls.mostRecent().args;
    expect(args[0]).toBe(1);
    expect((args[3] as { provider?: string })?.provider).toBe('anthropic');
  });

  it('onRecentModelChange sets filter signal and calls getRecent', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getRecent.calls.reset();

    c.onRecentModelChange('gpt-4o-mini');

    expect(c.recentModelFilter()).toBe('gpt-4o-mini');
    const args = svc.getRecent.calls.mostRecent().args;
    expect((args[3] as { model?: string })?.model).toBe('gpt-4o-mini');
  });

  it('onRecentFeatureChange sets filter signal and calls getRecent', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getRecent.calls.reset();

    c.onRecentFeatureChange('lesson_generation');

    expect(c.recentFeatureFilter()).toBe('lesson_generation');
    const args = svc.getRecent.calls.mostRecent().args;
    expect((args[3] as { featureKey?: string })?.featureKey).toBe('lesson_generation');
  });

  it('onRecentStatusChange sets filter signal and calls getRecent', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getRecent.calls.reset();

    c.onRecentStatusChange('failed');

    expect(c.recentStatusFilter()).toBe('failed');
    const args = svc.getRecent.calls.mostRecent().args;
    expect((args[3] as { status?: string })?.status).toBe('failed');
  });

  it('clearRecentFilters clears all filter signals and reloads page 1', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentProviderFilter.set('openai');
    c.recentStatusFilter.set('failed');
    c.recentPage.set(3);
    svc.getRecent.calls.reset();

    c.clearRecentFilters();

    expect(c.recentProviderFilter()).toBe('');
    expect(c.recentModelFilter()).toBe('');
    expect(c.recentFeatureFilter()).toBe('');
    expect(c.recentStatusFilter()).toBe('');
    expect(c.recentPage()).toBe(1);
    expect(svc.getRecent).toHaveBeenCalledTimes(1);
    const args = svc.getRecent.calls.mostRecent().args;
    expect((args[3] as { provider?: string })?.provider).toBeUndefined();
    expect((args[3] as { status?: string })?.status).toBeUndefined();
  });

  it('hasActiveRecentFilters is false when no filters set', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.hasActiveRecentFilters()).toBeFalse();
  });

  it('hasActiveRecentFilters is true when provider filter set', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentProviderFilter.set('openai');
    expect(c.hasActiveRecentFilters()).toBeTrue();
  });

  it('pagination preserves active filters when page changes', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentProviderFilter.set('anthropic');
    c.recentStatusFilter.set('success');
    svc.getRecent.calls.reset();

    c.onRecentPageChange(2);

    const args = svc.getRecent.calls.mostRecent().args;
    expect(args[0]).toBe(2);
    expect((args[3] as { provider?: string })?.provider).toBe('anthropic');
    expect((args[3] as { status?: string })?.status).toBe('success');
  });

  it('period change resets page to 1 and preserves active filters on reload', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentProviderFilter.set('openai');
    c.recentPage.set(3);
    svc.getRecent.calls.reset();

    c.onPeriodChange('7d');

    expect(c.recentPage()).toBe(1);
    const recentCall = svc.getRecent.calls.mostRecent();
    expect(recentCall.args[0]).toBe(1);
    expect((recentCall.args[3] as { provider?: string })?.provider).toBe('openai');
  });

  it('empty state renders when recentItems is empty after filter', () => {
    svc.getRecent.and.returnValue(of({ items: [], totalCount: 0, page: 1, pageSize: 25, totalPages: 1 }));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-empty-state')).toBeTruthy();
  });

  // ── student filter tests ────────────────────────────────────────────────────

  it('loadStudentOptions is called on init', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(adminApi.listStudents).toHaveBeenCalledWith({ pageSize: 50 });
  });

  it('studentOptions signal is populated from adminApi response', () => {
    adminApi.listStudents.and.returnValue(of({
      items: [
        { studentProfileId: 'uuid-1', email: 'alice@example.com', displayName: 'Alice' },
        { studentProfileId: 'uuid-2', email: 'bob@example.com', displayName: null },
      ],
      totalCount: 2, page: 1, pageSize: 50, totalPages: 1,
    } as any));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const opts = fixture.componentInstance.studentOptions();
    expect(opts.length).toBe(2);
    expect(opts[0].value).toBe('uuid-1');
    expect(opts[0].label).toContain('Alice');
    expect(opts[1].label).toBe('bob@example.com');
  });

  it('onRecentStudentChange sets studentFilter signal, resets page to 1, calls getRecent', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentPage.set(3);
    svc.getRecent.calls.reset();

    c.onRecentStudentChange('uuid-student-1');

    expect(c.recentStudentFilter()).toBe('uuid-student-1');
    expect(c.recentPage()).toBe(1);
    expect(svc.getRecent).toHaveBeenCalledTimes(1);
    const args = svc.getRecent.calls.mostRecent().args;
    expect((args[3] as { studentId?: string })?.studentId).toBe('uuid-student-1');
  });

  it('clearRecentFilters clears student filter signal', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentStudentFilter.set('uuid-student-1');
    svc.getRecent.calls.reset();

    c.clearRecentFilters();

    expect(c.recentStudentFilter()).toBe('');
    expect(c.recentStudentFilterValue).toBe('');
    const args = svc.getRecent.calls.mostRecent().args;
    expect((args[3] as { studentId?: string })?.studentId).toBeUndefined();
  });

  it('hasActiveRecentFilters is true when student filter set', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentStudentFilter.set('uuid-student-1');
    expect(c.hasActiveRecentFilters()).toBeTrue();
  });

  it('pagination preserves student filter when page changes', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentStudentFilter.set('uuid-student-1');
    svc.getRecent.calls.reset();

    c.onRecentPageChange(2);

    const args = svc.getRecent.calls.mostRecent().args;
    expect(args[0]).toBe(2);
    expect((args[3] as { studentId?: string })?.studentId).toBe('uuid-student-1');
  });

  it('service sends studentId param when student filter active', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getRecent.calls.reset();

    c.onRecentStudentChange('uuid-abc');

    const args = svc.getRecent.calls.mostRecent().args;
    expect((args[3] as { studentId?: string })?.studentId).toBe('uuid-abc');
  });

  it('existing provider filter still works alongside student filter', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentStudentFilter.set('uuid-student-1');
    svc.getRecent.calls.reset();

    c.onRecentProviderChange('openai');

    const args = svc.getRecent.calls.mostRecent().args;
    expect((args[3] as { provider?: string; studentId?: string })?.provider).toBe('openai');
    expect((args[3] as { studentId?: string })?.studentId).toBe('uuid-student-1');
  });

  // ── summary filter alignment tests (10U-7) ─────────────────────────────────

  it('onRecentProviderChange also reloads getSummary with provider filter', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getSummary.calls.reset();

    c.onRecentProviderChange('openai');

    expect(svc.getSummary).toHaveBeenCalledTimes(1);
    const summaryFilters = svc.getSummary.calls.mostRecent().args[1] as { provider?: string } | undefined;
    expect(summaryFilters?.provider).toBe('openai');
  });

  it('onRecentStatusChange also reloads getSummary with status filter', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getSummary.calls.reset();

    c.onRecentStatusChange('failed');

    expect(svc.getSummary).toHaveBeenCalledTimes(1);
    const summaryFilters = svc.getSummary.calls.mostRecent().args[1] as { status?: string } | undefined;
    expect(summaryFilters?.status).toBe('failed');
  });

  it('onRecentStudentChange also reloads getSummary with studentId filter', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getSummary.calls.reset();

    c.onRecentStudentChange('uuid-student-x');

    expect(svc.getSummary).toHaveBeenCalledTimes(1);
    const summaryFilters = svc.getSummary.calls.mostRecent().args[1] as { studentId?: string } | undefined;
    expect(summaryFilters?.studentId).toBe('uuid-student-x');
  });

  it('onRecentModelChange also reloads getSummary with model filter', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getSummary.calls.reset();

    c.onRecentModelChange('gpt-4o-mini');

    expect(svc.getSummary).toHaveBeenCalledTimes(1);
    const summaryFilters = svc.getSummary.calls.mostRecent().args[1] as { model?: string } | undefined;
    expect(summaryFilters?.model).toBe('gpt-4o-mini');
  });

  it('onRecentFeatureChange also reloads getSummary with featureKey filter', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getSummary.calls.reset();

    c.onRecentFeatureChange('lesson_generation');

    expect(svc.getSummary).toHaveBeenCalledTimes(1);
    const summaryFilters = svc.getSummary.calls.mostRecent().args[1] as { featureKey?: string } | undefined;
    expect(summaryFilters?.featureKey).toBe('lesson_generation');
  });

  it('clearRecentFilters reloads getSummary with no column filters', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentProviderFilter.set('openai');
    c.recentStatusFilter.set('failed');
    svc.getSummary.calls.reset();

    c.clearRecentFilters();

    expect(svc.getSummary).toHaveBeenCalledTimes(1);
    const summaryFilters = svc.getSummary.calls.mostRecent().args[1] as { provider?: string; status?: string } | undefined;
    expect(summaryFilters?.provider).toBeUndefined();
    expect(summaryFilters?.status).toBeUndefined();
  });

  it('clearRecentFilters does not change the date period', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.onPeriodChange('7d');
    svc.getSummary.calls.reset();

    c.clearRecentFilters();

    // period preset unchanged — range arg should still have a from date
    const range = svc.getSummary.calls.mostRecent().args[0] as { from?: string } | undefined;
    expect(range?.from).toBeDefined();
  });

  it('period change reloads getSummary and getRecent together', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getSummary.calls.reset();
    svc.getRecent.calls.reset();

    fixture.componentInstance.onPeriodChange('30d');

    expect(svc.getSummary).toHaveBeenCalledTimes(1);
    expect(svc.getRecent).toHaveBeenCalledTimes(1);
  });

  it('kpi cards still render after filter alignment change', () => {
    svc.getSummary.and.returnValue(of(makeSummary({ totalCalls: 5, totalInputTokens: 999 })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-kpi-card');
    expect(cards.length).toBeGreaterThanOrEqual(4);
    expect(fixture.componentInstance.summary()!.totalInputTokens).toBe(999);
  });

  it('getSummary receives both date range and column filters when both active', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.onPeriodChange('7d');
    svc.getSummary.calls.reset();

    c.recentProviderFilter.set('anthropic');
    c.onRecentStatusChange('success');

    const call = svc.getSummary.calls.mostRecent();
    const range   = call.args[0] as { from?: string } | undefined;
    const filters = call.args[1] as { provider?: string; status?: string } | undefined;
    expect(range?.from).toBeDefined();
    expect(filters?.status).toBe('success');
    expect(filters?.provider).toBe('anthropic');
  });

  // ── export CSV tests (10U-8) ───────────────────────────────────────────────

  it('exportCsv calls exportUsageCsv with current filters', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentProviderFilter.set('openai');
    c.recentStatusFilter.set('success');

    c.exportCsv();

    expect(svc.exportUsageCsv).toHaveBeenCalledTimes(1);
    const args = svc.exportUsageCsv.calls.mostRecent().args;
    const filters = args[1] as { provider?: string; status?: string } | undefined;
    expect(filters?.provider).toBe('openai');
    expect(filters?.status).toBe('success');
  });

  it('exportCsv sends date range but no page or pageSize', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.onPeriodChange('7d');

    c.exportCsv();

    const args = svc.exportUsageCsv.calls.mostRecent().args;
    const range = args[0] as { from?: string } | undefined;
    expect(range?.from).toBeDefined();
    // exportUsageCsv takes only (range, filters) — no page/pageSize
    expect(args.length).toBe(2);
  });

  it('exportCsv sets exporting signal to true then false on success', () => {
    // Stub a synchronous observable so we can check state transitions
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    // After exportCsv resolves (sync observable), exporting should be false
    c.exportCsv();
    expect(c.exporting()).toBeFalse();
  });

  it('export button is disabled while exporting', () => {
    // Make exportUsageCsv return a never-resolving observable to hold exporting=true
    svc.exportUsageCsv.and.returnValue(new Subject<Blob>());
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.exportCsv();
    fixture.detectChanges();

    const btn = Array.from(fixture.nativeElement.querySelectorAll('sp-admin-button') as NodeListOf<Element>)
      .find(b => b.textContent?.includes('Exporting'));
    expect(btn).toBeTruthy();
  });

  it('exportCsv shows error alert on failure', () => {
    svc.exportUsageCsv.and.returnValue(throwError(() => ({ error: { error: 'Export failed' } })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.exportCsv();
    fixture.detectChanges();

    expect(c.exportError()).toBe('Export failed');
    expect(fixture.nativeElement.querySelector('sp-admin-alert')).toBeTruthy();
  });

  it('exportCsv passes student filter to service', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentStudentFilter.set('uuid-student-export');

    c.exportCsv();

    const args = svc.exportUsageCsv.calls.mostRecent().args;
    const filters = args[1] as { studentId?: string } | undefined;
    expect(filters?.studentId).toBe('uuid-student-export');
  });

  // ── trend tests (10U-9) ────────────────────────────────────────────────────

  it('getTrends is called on init', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(svc.getTrends).toHaveBeenCalledTimes(1);
  });

  it('trend section shows empty state when no buckets', () => {
    svc.getTrends.and.returnValue(of([]));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.trendBuckets().length).toBe(0);
    expect(fixture.nativeElement.querySelector('sp-admin-empty-state')).toBeTruthy();
  });

  it('trend section shows buckets when data loaded', () => {
    const buckets: AiUsageTrendBucket[] = [
      { date: '2025-03-10', callCount: 5, successCount: 4, failureCount: 1, fallbackCount: 0, inputTokens: 100, outputTokens: 50, totalTokens: 150, costUsd: 0.01 },
      { date: '2025-03-11', callCount: 3, successCount: 3, failureCount: 0, fallbackCount: 0, inputTokens: 60, outputTokens: 30, totalTokens: 90, costUsd: 0.005 },
    ];
    svc.getTrends.and.returnValue(of(buckets));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.trendBuckets().length).toBe(2);
    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBeGreaterThanOrEqual(2);
  });

  it('getTrends is called with current filters', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getTrends.calls.reset();

    c.recentProviderFilter.set('openai');
    c.onRecentStatusChange('success');

    const args = svc.getTrends.calls.mostRecent().args;
    const filters = args[1] as { provider?: string; status?: string } | undefined;
    expect(filters?.provider).toBe('openai');
    expect(filters?.status).toBe('success');
  });

  it('onRecentProviderChange reloads getTrends', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getTrends.calls.reset();

    fixture.componentInstance.onRecentProviderChange('anthropic');

    expect(svc.getTrends).toHaveBeenCalledTimes(1);
  });

  it('onPeriodChange reloads getTrends', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getTrends.calls.reset();

    fixture.componentInstance.onPeriodChange('7d');

    expect(svc.getTrends).toHaveBeenCalledTimes(1);
  });

  it('clearRecentFilters reloads getTrends', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getTrends.calls.reset();

    fixture.componentInstance.clearRecentFilters();

    expect(svc.getTrends).toHaveBeenCalledTimes(1);
  });

  it('getTrends sends date range from period preset', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getTrends.calls.reset();

    fixture.componentInstance.onPeriodChange('7d');

    const range = svc.getTrends.calls.mostRecent().args[0] as { from?: string } | undefined;
    expect(range?.from).toBeDefined();
  });

  it('trend loading state sets loadingTrends signal', () => {
    svc.getTrends.and.returnValue(new Subject<AiUsageTrendBucket[]>());
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.loadingTrends()).toBeTrue();
  });

  it('trend error state sets trendError signal', () => {
    svc.getTrends.and.returnValue(throwError(() => ({ error: { error: 'Trend error' } })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.trendError()).toBe('Trend error');
  });

  it('trend section shows error state on failure', () => {
    svc.getTrends.and.returnValue(throwError(() => ({ error: { error: 'Trend error' } })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('sp-admin-error-state')).toBeTruthy();
  });

  // ── custom date range tests (10U-10) ──────────────────────────────────────

  it('Custom range option exists in periodOptions', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const opts = fixture.componentInstance.periodOptions;
    expect(opts.some(o => o.value === 'custom')).toBeTrue();
  });

  it('selecting Custom range does not call load immediately', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getSummary.calls.reset();
    svc.getRecent.calls.reset();
    svc.getTrends.calls.reset();

    fixture.componentInstance.onPeriodChange('custom');

    expect(svc.getSummary).not.toHaveBeenCalled();
    expect(svc.getRecent).not.toHaveBeenCalled();
    expect(svc.getTrends).not.toHaveBeenCalled();
  });

  it('applyCustomRange with missing from shows validation error and does not call API', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getSummary.calls.reset();

    c.onPeriodChange('custom');
    c.customFrom.set('');
    c.customTo.set('2025-03-15');
    c.applyCustomRange();

    expect(c.customRangeError()).toBeTruthy();
    expect(svc.getSummary).not.toHaveBeenCalled();
  });

  it('applyCustomRange with missing to shows validation error and does not call API', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getSummary.calls.reset();

    c.onPeriodChange('custom');
    c.customFrom.set('2025-03-10');
    c.customTo.set('');
    c.applyCustomRange();

    expect(c.customRangeError()).toBeTruthy();
    expect(svc.getSummary).not.toHaveBeenCalled();
  });

  it('applyCustomRange with from after to shows validation error', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getSummary.calls.reset();

    c.onPeriodChange('custom');
    c.customFrom.set('2025-03-20');
    c.customTo.set('2025-03-10');
    c.applyCustomRange();

    expect(c.customRangeError()).toBeTruthy();
    expect(svc.getSummary).not.toHaveBeenCalled();
  });

  it('applyCustomRange with valid range calls summary/recent/trends', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getSummary.calls.reset();
    svc.getRecent.calls.reset();
    svc.getTrends.calls.reset();

    c.onPeriodChange('custom');
    c.customFrom.set('2025-03-10');
    c.customTo.set('2025-03-15');
    c.applyCustomRange();

    expect(c.customRangeError()).toBe('');
    expect(svc.getSummary).toHaveBeenCalledTimes(1);
    expect(svc.getRecent).toHaveBeenCalledTimes(1);
    expect(svc.getTrends).toHaveBeenCalledTimes(1);
  });

  it('applyCustomRange resets recent page to 1', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    c.recentPage.set(3);

    c.onPeriodChange('custom');
    c.customFrom.set('2025-03-10');
    c.customTo.set('2025-03-15');
    c.applyCustomRange();

    expect(c.recentPage()).toBe(1);
  });

  it('applyCustomRange passes from/to in the date range argument', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getSummary.calls.reset();

    c.onPeriodChange('custom');
    c.customFrom.set('2025-03-10');
    c.customTo.set('2025-03-15');
    c.applyCustomRange();

    const range = svc.getSummary.calls.mostRecent().args[0] as { from?: string; to?: string } | undefined;
    expect(range?.from).toContain('2025-03-10');
    expect(range?.to).toContain('2025-03-16'); // exclusive upper bound: day+1
  });

  it('buildRange for custom returns undefined when from or to missing', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.periodPreset.set('custom');
    c.customFrom.set('2025-03-10');
    c.customTo.set('');

    expect(c.buildRange('custom')).toBeUndefined();
  });

  it('exportCsv uses custom date range when custom preset active', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.onPeriodChange('custom');
    c.customFrom.set('2025-03-10');
    c.customTo.set('2025-03-15');
    c.applyCustomRange();
    svc.exportUsageCsv.calls.reset();

    c.exportCsv();

    const range = svc.exportUsageCsv.calls.mostRecent().args[0] as { from?: string; to?: string } | undefined;
    expect(range?.from).toContain('2025-03-10');
  });

  it('switching from custom to preset hides date error and reloads', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.onPeriodChange('custom');
    c.customFrom.set('2025-03-20');
    c.customTo.set('2025-03-10');
    c.applyCustomRange();
    expect(c.customRangeError()).toBeTruthy();

    svc.getSummary.calls.reset();
    c.onPeriodChange('7d');

    expect(c.customRangeError()).toBe('');
    expect(svc.getSummary).toHaveBeenCalledTimes(1);
  });

  it('clearCustomRange clears from/to and error', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.onPeriodChange('custom');
    c.customFrom.set('2025-03-10');
    c.customTo.set('2025-03-15');
    c.customRangeError.set('some error');

    c.clearCustomRange();

    expect(c.customFrom()).toBe('');
    expect(c.customTo()).toBe('');
    expect(c.customRangeError()).toBe('');
  });

  it('existing preset period still calls load correctly after custom was used', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.onPeriodChange('custom');
    c.customFrom.set('2025-03-10');
    c.customTo.set('2025-03-15');
    c.applyCustomRange();

    svc.getSummary.calls.reset();
    c.onPeriodChange('30d');

    const range = svc.getSummary.calls.mostRecent().args[0] as { from?: string } | undefined;
    expect(range?.from).toBeDefined();
    expect(c.customRangeError()).toBe('');
  });

  it('column filters still apply alongside custom date range', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getSummary.calls.reset();

    c.onPeriodChange('custom');
    c.customFrom.set('2025-03-10');
    c.customTo.set('2025-03-15');
    c.recentProviderFilter.set('openai');
    c.applyCustomRange();

    const filters = svc.getSummary.calls.mostRecent().args[1] as { provider?: string } | undefined;
    expect(filters?.provider).toBe('openai');
  });

  // ── REDESIGN-6: KPI strip, period pills, trend bars, not-impl placeholders ─

  it('summary strip has aria-label "AI usage summary"', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const strip = (fixture.nativeElement as HTMLElement).querySelector('[aria-label="AI usage summary"]');
    expect(strip).toBeTruthy();
  });

  it('kpiSummary computed returns correct totalCalls', () => {
    svc.getSummary.and.returnValue(of(makeSummary({ totalCalls: 42 })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.kpiSummary()!.totalCalls).toBe(42);
  });

  it('kpiSummary computed returns correct totalCostUsd', () => {
    svc.getSummary.and.returnValue(of(makeSummary({ totalCostUsd: 1.2345 })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.kpiSummary()!.totalCostUsd).toBeCloseTo(1.2345);
  });

  it('kpiSummary computed calculates successRate correctly', () => {
    svc.getSummary.and.returnValue(of(makeSummary({ totalCalls: 10, successfulCalls: 8 })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.kpiSummary()!.successRate).toBe(80);
  });

  it('kpiSummary successRate is 0 when totalCalls is 0', () => {
    svc.getSummary.and.returnValue(of(makeSummary({ totalCalls: 0, successfulCalls: 0 })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.kpiSummary()!.successRate).toBe(0);
  });

  it('kpiSummary returns null when summary not loaded', () => {
    svc.getSummary.and.returnValue(new Subject<AiUsageSummary>().asObservable());
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.kpiSummary()).toBeNull();
  });

  it('period pill buttons render', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const pills = (fixture.nativeElement as HTMLElement).querySelectorAll('.sp-admin-period-pill');
    expect(pills.length).toBeGreaterThanOrEqual(3);
  });

  it('period pill container has aria-label', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const container = (fixture.nativeElement as HTMLElement).querySelector('[aria-label="Quick date range selection"]');
    expect(container).toBeTruthy();
  });

  it('onPillClick updates periodPreset and calls load', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();
    svc.getSummary.calls.reset();

    c.onPillClick('7d');

    expect(c.periodPreset()).toBe('7d');
    expect(svc.getSummary).toHaveBeenCalledTimes(1);
  });

  it('trendBars returns empty array when no buckets', () => {
    svc.getTrends.and.returnValue(of([]));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(fixture.componentInstance.trendBars().length).toBe(0);
  });

  it('trendBars returns proportional heights from real data', () => {
    const buckets: AiUsageTrendBucket[] = [
      { date: '2025-03-10', callCount: 10, successCount: 9, failureCount: 1, fallbackCount: 0, inputTokens: 100, outputTokens: 50, totalTokens: 150, costUsd: 0.01 },
      { date: '2025-03-11', callCount: 5,  successCount: 5, failureCount: 0, fallbackCount: 0, inputTokens:  50, outputTokens: 25, totalTokens:  75, costUsd: 0.005 },
    ];
    svc.getTrends.and.returnValue(of(buckets));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const bars = fixture.componentInstance.trendBars();
    expect(bars.length).toBe(2);
    expect(bars[0].height).toBe(48);
    expect(bars[1].height).toBeGreaterThan(0);
    expect(bars[1].height).toBeLessThan(48);
  });

  it('calls over time area chart renders when trend data available', () => {
    const buckets: AiUsageTrendBucket[] = [
      { date: '2025-03-10', callCount: 5, successCount: 4, failureCount: 1, fallbackCount: 0, inputTokens: 100, outputTokens: 50, totalTokens: 150, costUsd: 0.01 },
      { date: '2025-03-11', callCount: 7, successCount: 7, failureCount: 0, fallbackCount: 0, inputTokens: 120, outputTokens: 60, totalTokens: 180, costUsd: 0.012 },
    ];
    svc.getTrends.and.returnValue(of(buckets));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const chart = (fixture.nativeElement as HTMLElement).querySelector('sp-admin-area-chart');
    expect(chart).toBeTruthy();
  });

  it('AI calls per day graph card renders', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const el = (fixture.nativeElement as HTMLElement).querySelector('sp-admin-graph-card');
    expect(el).toBeTruthy();
  });

  it('calls by feature graph card renders', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const els = (fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-graph-card');
    expect(els.length).toBeGreaterThanOrEqual(1);
  });

  it('no API key or secret text rendered anywhere', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text.toLowerCase()).not.toContain('api_key');
    expect(text.toLowerCase()).not.toContain('bearer ');
    expect(text.toLowerCase()).not.toContain('sk-');
  });

  // ── zero-cost alert ────────────────────────────────────────────────────────

  it('zero-cost alert renders when zeroCostCallCount > 0', () => {
    svc.getSummary.and.returnValue(of(makeSummary({ zeroCostCallCount: 2, zeroCostTotalTokens: 500 })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const alert = (fixture.nativeElement as HTMLElement).querySelector('sp-admin-alert[variant="warning"]');
    expect(alert).toBeTruthy();
  });

  it('zero-cost alert is absent when zeroCostCallCount is 0', () => {
    svc.getSummary.and.returnValue(of(makeSummary({ zeroCostCallCount: 0, zeroCostTotalTokens: 0 })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const alert = (fixture.nativeElement as HTMLElement).querySelector('sp-admin-alert[variant="warning"]');
    expect(alert).toBeNull();
  });

  it('zero-cost alert is absent when summary is not yet loaded', () => {
    svc.getSummary.and.returnValue(new Subject<AiUsageSummary>().asObservable());
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const alert = (fixture.nativeElement as HTMLElement).querySelector('sp-admin-alert[variant="warning"]');
    expect(alert).toBeNull();
  });

  it('zero-cost alert updates when filter reload returns non-zero count', () => {
    svc.getSummary.and.returnValue(of(makeSummary({ zeroCostCallCount: 0, zeroCostTotalTokens: 0 })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    svc.getSummary.and.returnValue(of(makeSummary({ zeroCostCallCount: 4, zeroCostTotalTokens: 1200 })));
    c.onRecentProviderChange('openai');
    fixture.detectChanges();

    const alert = (fixture.nativeElement as HTMLElement).querySelector('sp-admin-alert[variant="warning"]');
    expect(alert).toBeTruthy();
  });

  it('zero-cost alert disappears when filter reload returns zero count', () => {
    svc.getSummary.and.returnValue(of(makeSummary({ zeroCostCallCount: 3, zeroCostTotalTokens: 900 })));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    svc.getSummary.and.returnValue(of(makeSummary({ zeroCostCallCount: 0, zeroCostTotalTokens: 0 })));
    c.onRecentProviderChange('anthropic');
    fixture.detectChanges();

    const alert = (fixture.nativeElement as HTMLElement).querySelector('sp-admin-alert[variant="warning"]');
    expect(alert).toBeNull();
  });
});
