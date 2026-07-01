import { TestBed } from '@angular/core/testing';
import { FeedbackPendingStateComponent } from './feedback-pending-state.component';

describe('FeedbackPendingStateComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [FeedbackPendingStateComponent] });
  });

  function create(status: string, label: 'speaking' | 'writing' = 'speaking') {
    const fixture = TestBed.createComponent(FeedbackPendingStateComponent);
    fixture.componentInstance.status = status as any;
    fixture.componentInstance.label = label;
    fixture.detectChanges();
    return fixture;
  }

  it('shows pending message for Pending status', () => {
    const { nativeElement } = create('Pending');
    expect(nativeElement.querySelector('[data-testid="feedback-pending-state"]')).toBeTruthy();
    expect(nativeElement.querySelector('[data-testid="pending-failed"]')).toBeNull();
  });

  it('shows pending message for Evaluating status', () => {
    const { nativeElement } = create('Evaluating');
    expect(nativeElement.querySelector('[data-testid="pending-failed"]')).toBeNull();
  });

  it('shows failed message for Failed status', () => {
    const { nativeElement } = create('Failed');
    expect(nativeElement.querySelector('[data-testid="pending-failed"]')).toBeTruthy();
  });

  it('shows not-supported message for NotSupported status', () => {
    const { nativeElement } = create('NotSupported');
    expect(nativeElement.querySelector('[data-testid="pending-not-supported"]')).toBeTruthy();
  });

  it('uses the label in the message text', () => {
    const { nativeElement } = create('Pending', 'writing');
    expect(nativeElement.querySelector('[data-testid="feedback-pending-state"]').textContent).toContain('writing');
  });

  it('shows not-supported for Skipped status', () => {
    const fixture = create('Skipped');
    expect(fixture.componentInstance.isFailed).toBe(false);
  });
});
