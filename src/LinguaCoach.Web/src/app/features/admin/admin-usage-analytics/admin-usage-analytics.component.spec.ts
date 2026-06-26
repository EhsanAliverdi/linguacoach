import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AdminUsageAnalyticsComponent } from './admin-usage-analytics.component';
import { AdminApiService } from '../../../core/services/admin.api.service';
import { AdminAiUsageTrendResponse, AdminDashboardActivityTrendResponse } from '../../../core/models/admin.models';

const AI_TRENDS: AdminAiUsageTrendResponse = {
  period: '30d',
  buckets: [
    { date: '2026-06-01', requestCount: 10, successfulCalls: 9, failedCalls: 1, inputTokens: 500, outputTokens: 200, totalTokens: 700, cost: 0.012 },
    { date: '2026-06-02', requestCount: 5,  successfulCalls: 5, failedCalls: 0, inputTokens: 250, outputTokens: 100, totalTokens: 350, cost: 0.006 },
  ],
};

const ACTIVITY_TRENDS: AdminDashboardActivityTrendResponse = {
  period: '30d',
  buckets: [
    { date: '2026-06-01', activityCount: 8, completedCount: 7, failedCount: 1 },
    { date: '2026-06-02', activityCount: 4, completedCount: 4, failedCount: 0 },
  ],
};

const EMPTY_AI: AdminAiUsageTrendResponse = { period: '30d', buckets: [] };
const EMPTY_ACTIVITY: AdminDashboardActivityTrendResponse = { period: '30d', buckets: [] };

function makeApi(
  ai: AdminAiUsageTrendResponse | 'error' = EMPTY_AI,
  activity: AdminDashboardActivityTrendResponse | 'error' = EMPTY_ACTIVITY,
) {
  return {
    getAiUsageTrends: jasmine.createSpy('getAiUsageTrends').and.returnValue(
      ai === 'error' ? throwError(() => new Error('fail')) : of(ai),
    ),
    getDashboardActivityTrends: jasmine.createSpy('getDashboardActivityTrends').and.returnValue(
      activity === 'error' ? throwError(() => new Error('fail')) : of(activity),
    ),
  };
}

describe('AdminUsageAnalyticsComponent', () => {
  let fixture: ComponentFixture<AdminUsageAnalyticsComponent>;
  let component: AdminUsageAnalyticsComponent;
  let api: ReturnType<typeof makeApi>;

  async function setup(
    ai: AdminAiUsageTrendResponse | 'error' = EMPTY_AI,
    activity: AdminDashboardActivityTrendResponse | 'error' = EMPTY_ACTIVITY,
  ) {
    api = makeApi(ai, activity);
    await TestBed.configureTestingModule({
      imports: [AdminUsageAnalyticsComponent],
      providers: [
        provideRouter([]),
        { provide: AdminApiService, useValue: api },
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(AdminUsageAnalyticsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  it('renders the usage analytics page', async () => {
    await setup();
    expect(fixture.nativeElement.textContent).toContain('Usage');
  });

  it('calls getAiUsageTrends and getDashboardActivityTrends on init', async () => {
    await setup();
    expect(api.getAiUsageTrends).toHaveBeenCalledTimes(1);
    expect(api.getDashboardActivityTrends).toHaveBeenCalledTimes(1);
  });

  it('totalCost sums cost from AI trend buckets', async () => {
    await setup(AI_TRENDS);
    expect(component.totalCost()).toBeCloseTo(0.018, 5);
  });

  it('totalCalls sums requestCount from AI trend buckets', async () => {
    await setup(AI_TRENDS);
    expect(component.totalCalls()).toBe(15);
  });

  it('totalActivities sums activityCount from activity trend buckets', async () => {
    await setup(EMPTY_AI, ACTIVITY_TRENDS);
    expect(component.totalActivities()).toBe(12);
  });

  it('avgCostPerStudent is null — not-implemented', async () => {
    await setup(AI_TRENDS);
    expect(component.avgCostPerStudent()).toBeNull();
  });

  it('avgCostDisplay shows N/A when avgCostPerStudent is null', async () => {
    await setup(AI_TRENDS);
    expect(component.avgCostDisplay()).toBe('N/A');
  });

  it('shows error state when AI trends API fails', async () => {
    await setup('error');
    expect(component.aiTrendsError()).toBeTruthy();
    expect(component.loadingAiTrends()).toBeFalse();
  });

  it('shows error state when activity trends API fails', async () => {
    await setup(EMPTY_AI, 'error');
    expect(component.activityTrendsError()).toBeTruthy();
    expect(component.loadingActivityTrends()).toBeFalse();
  });

  it('setPeriod updates period signal and reloads', async () => {
    await setup();
    api.getAiUsageTrends.calls.reset();
    api.getDashboardActivityTrends.calls.reset();
    component.setPeriod('7d');
    expect(component.period()).toBe('7d');
    expect(api.getAiUsageTrends).toHaveBeenCalledTimes(1);
  });

  it('setPeriod to custom does not trigger load', async () => {
    await setup();
    api.getAiUsageTrends.calls.reset();
    component.setPeriod('custom');
    expect(api.getAiUsageTrends).not.toHaveBeenCalled();
  });

  it('applyCustomRange shows error when dates are missing', async () => {
    await setup();
    component.customFrom.set('');
    component.customTo.set('');
    component.applyCustomRange();
    expect(component.customRangeError()).toBeTruthy();
  });

  it('applyCustomRange shows error when from is after to', async () => {
    await setup();
    component.customFrom.set('2026-06-10');
    component.customTo.set('2026-06-01');
    component.applyCustomRange();
    expect(component.customRangeError()).toContain('before');
  });

  it('aiCostChartData computed maps cost from AI buckets', async () => {
    await setup(AI_TRENDS);
    const data = component.aiCostChartData();
    expect(data.length).toBe(2);
    expect(data[0]).toBeCloseTo(0.012, 5);
  });
});
