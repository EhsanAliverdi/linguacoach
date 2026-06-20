import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminAiUsageComponent, PeriodPreset } from './admin-ai-usage.component';
import { AiUsageService, AiUsageSummary, AiUsageRecentItem } from '../../../core/services/ai-usage.service';

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

  beforeEach(() => {
    svc = jasmine.createSpyObj('AiUsageService', ['getSummary', 'getRecent']);
    svc.getSummary.and.returnValue(of(makeSummary()));
    svc.getRecent.and.returnValue(of({ items: [makeRecentItem()], totalCount: 1, page: 1, pageSize: 25, totalPages: 1 }));

    TestBed.configureTestingModule({
      imports: [AdminAiUsageComponent],
      providers: [{ provide: AiUsageService, useValue: svc }],
    });
  });

  it('renders page header', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).querySelector('sp-admin-page-header')).toBeTruthy();
  });

  it('renders stat cards after summary loads', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const cards = (fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-stat-card');
    expect(cards.length).toBe(8);
  });

  it('renders input token stat card', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const cards = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-stat-card'));
    const labels = cards.map(c => c.getAttribute('label') ?? c.getAttribute('ng-reflect-label') ?? c.textContent ?? '');
    expect(labels.some(l => l.toLowerCase().includes('input token'))).toBeTrue();
  });

  it('renders output token stat card', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const cards = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-stat-card'));
    const labels = cards.map(c => c.getAttribute('label') ?? c.getAttribute('ng-reflect-label') ?? c.textContent ?? '');
    expect(labels.some(l => l.toLowerCase().includes('output token'))).toBeTrue();
  });

  it('renders total token stat card', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    const cards = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('sp-admin-stat-card'));
    const labels = cards.map(c => c.getAttribute('label') ?? c.getAttribute('ng-reflect-label') ?? c.textContent ?? '');
    expect(labels.some(l => l.toLowerCase().includes('total token'))).toBeTrue();
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

  it('filters recent calls by provider', () => {
    svc.getRecent.and.returnValue(of({ items: [
      makeRecentItem({ id: '1', provider: 'anthropic' }),
      makeRecentItem({ id: '2', provider: 'openai' }),
    ], totalCount: 2, page: 1, pageSize: 25, totalPages: 1 }));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.onProviderFilterChange('openai');
    fixture.detectChanges();
    expect(c.filteredRecentItems().length).toBe(1);
    expect(c.filteredRecentItems()[0].provider).toBe('openai');
  });

  it('filters recent calls by status: failed', () => {
    svc.getRecent.and.returnValue(of({ items: [
      makeRecentItem({ id: '1', wasSuccessful: true }),
      makeRecentItem({ id: '2', wasSuccessful: false }),
    ], totalCount: 2, page: 1, pageSize: 25, totalPages: 1 }));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.onStatusFilterChange('failed');
    expect(c.filteredRecentItems().length).toBe(1);
    expect(c.filteredRecentItems()[0].wasSuccessful).toBeFalse();
  });

  it('filters recent calls by status: fallback', () => {
    svc.getRecent.and.returnValue(of({ items: [
      makeRecentItem({ id: '1', isFallback: false }),
      makeRecentItem({ id: '2', isFallback: true }),
    ], totalCount: 2, page: 1, pageSize: 25, totalPages: 1 }));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.onStatusFilterChange('fallback');
    expect(c.filteredRecentItems().length).toBe(1);
    expect(c.filteredRecentItems()[0].isFallback).toBeTrue();
  });

  it('resets page to 1 when provider filter changes', () => {
    svc.getRecent.and.returnValue(of({ items: [makeRecentItem()], totalCount: 1, page: 1, pageSize: 25, totalPages: 1 }));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.recentPage.set(2);
    c.onProviderFilterChange('anthropic');
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
    // 'all' preset → no range → service called with undefined
    expect(svc.getSummary).toHaveBeenCalledWith(undefined);
  });

  it('default load calls getRecent without date params', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    expect(svc.getRecent).toHaveBeenCalledWith(1, 25, undefined);
  });

  it('onPeriodChange to last7days passes a from date to getSummary', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getSummary.calls.reset();
    fixture.componentInstance.onPeriodChange('7d');
    const args = svc.getSummary.calls.mostRecent().args[0] as { from?: string } | undefined;
    expect(args).toBeTruthy();
    expect(args!.from).toBeDefined();
    // from should be a valid ISO string in the past
    const from = new Date(args!.from!);
    expect(from.getTime()).toBeLessThan(Date.now());
  });

  it('onPeriodChange to last30days passes a from date approximately 30 days ago', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getSummary.calls.reset();
    const before = Date.now();
    fixture.componentInstance.onPeriodChange('30d');
    const args = svc.getSummary.calls.mostRecent().args[0] as { from?: string } | undefined;
    expect(args?.from).toBeDefined();
    const fromMs = new Date(args!.from!).getTime();
    const expectedMs = before - 30 * 24 * 60 * 60 * 1000;
    // Allow 5s tolerance for test execution time
    expect(Math.abs(fromMs - expectedMs)).toBeLessThan(5000);
  });

  it('onPeriodChange to today passes UTC start of today', () => {
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    fixture.detectChanges();
    svc.getSummary.calls.reset();
    fixture.componentInstance.onPeriodChange('today');
    const args = svc.getSummary.calls.mostRecent().args[0] as { from?: string } | undefined;
    expect(args?.from).toBeDefined();
    const from = new Date(args!.from!);
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
    const args = svc.getSummary.calls.mostRecent().args[0] as { from?: string } | undefined;
    expect(args?.from).toBeDefined();
    const from = new Date(args!.from!);
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
    expect(svc.getRecent).toHaveBeenCalledWith(1, 25, undefined);
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
    expect(svc.getRecent).toHaveBeenCalledWith(3, 25, undefined);
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
});
