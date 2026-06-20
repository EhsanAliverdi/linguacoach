import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { AdminAiUsageComponent } from './admin-ai-usage.component';
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
    svc.getRecent.and.returnValue(of({ total: 1, items: [makeRecentItem()] }));

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
    svc.getRecent.and.returnValue(of({ total: 2, items: [
      makeRecentItem({ id: '1', provider: 'anthropic' }),
      makeRecentItem({ id: '2', provider: 'openai' }),
    ]}));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.onProviderFilterChange('openai');
    fixture.detectChanges();
    expect(c.filteredRecentItems().length).toBe(1);
    expect(c.filteredRecentItems()[0].provider).toBe('openai');
  });

  it('filters recent calls by status: failed', () => {
    svc.getRecent.and.returnValue(of({ total: 2, items: [
      makeRecentItem({ id: '1', wasSuccessful: true }),
      makeRecentItem({ id: '2', wasSuccessful: false }),
    ]}));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.onStatusFilterChange('failed');
    expect(c.filteredRecentItems().length).toBe(1);
    expect(c.filteredRecentItems()[0].wasSuccessful).toBeFalse();
  });

  it('filters recent calls by status: fallback', () => {
    svc.getRecent.and.returnValue(of({ total: 2, items: [
      makeRecentItem({ id: '1', isFallback: false }),
      makeRecentItem({ id: '2', isFallback: true }),
    ]}));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    c.onStatusFilterChange('fallback');
    expect(c.filteredRecentItems().length).toBe(1);
    expect(c.filteredRecentItems()[0].isFallback).toBeTrue();
  });

  it('resets page to 1 when provider filter changes', () => {
    svc.getRecent.and.returnValue(of({ total: 1, items: [makeRecentItem()] }));
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
    svc.getRecent.and.returnValue(of({ total: 0, items: [] }));
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
    svc.getRecent.and.returnValue(of({ total: 3, items: [
      makeRecentItem({ id: '1', provider: 'openai' }),
      makeRecentItem({ id: '2', provider: 'anthropic' }),
      makeRecentItem({ id: '3', provider: 'openai' }),
    ]}));
    const fixture = TestBed.createComponent(AdminAiUsageComponent);
    const c = fixture.componentInstance;
    fixture.detectChanges();

    const opts = c.providerOptions();
    expect(opts.length).toBe(2);
    expect(opts[0].value).toBe('anthropic');
    expect(opts[1].value).toBe('openai');
  });
});
