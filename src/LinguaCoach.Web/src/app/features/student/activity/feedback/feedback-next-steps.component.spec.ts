import { TestBed } from '@angular/core/testing';
import { FeedbackNextStepsComponent } from './feedback-next-steps.component';

describe('FeedbackNextStepsComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [FeedbackNextStepsComponent] });
  });

  function create(activityType: string | null = null) {
    const fixture = TestBed.createComponent(FeedbackNextStepsComponent);
    fixture.componentInstance.activityType = activityType as any;
    fixture.detectChanges();
    return fixture;
  }

  it('always shows Next activity and Back to dashboard buttons', () => {
    const { nativeElement } = create();
    expect(nativeElement.querySelector('[data-testid="btn-next-activity"]')).toBeTruthy();
    expect(nativeElement.querySelector('[data-testid="btn-back-dashboard"]')).toBeTruthy();
  });

  it('shows Improve my answer button for writingScenario', () => {
    const { nativeElement } = create('writingScenario');
    expect(nativeElement.querySelector('[data-testid="btn-improve"]')).toBeTruthy();
  });

  it('hides Improve my answer for speakingRolePlay', () => {
    const { nativeElement } = create('speakingRolePlay');
    expect(nativeElement.querySelector('[data-testid="btn-improve"]')).toBeNull();
  });

  it('hides Improve my answer for null activityType', () => {
    const { nativeElement } = create(null);
    expect(nativeElement.querySelector('[data-testid="btn-improve"]')).toBeNull();
  });

  it('hides Try again for speakingRolePlay', () => {
    const { nativeElement } = create('speakingRolePlay');
    expect(nativeElement.querySelector('[data-testid="btn-try-again"]')).toBeNull();
  });

  it('hides Try again for pronunciationPractice', () => {
    const { nativeElement } = create('pronunciationPractice');
    expect(nativeElement.querySelector('[data-testid="btn-try-again"]')).toBeNull();
  });

  it('shows Try again for writingScenario', () => {
    const { nativeElement } = create('writingScenario');
    expect(nativeElement.querySelector('[data-testid="btn-try-again"]')).toBeTruthy();
  });

  it('shows Try again for listeningComprehension', () => {
    const { nativeElement } = create('listeningComprehension');
    expect(nativeElement.querySelector('[data-testid="btn-try-again"]')).toBeTruthy();
  });

  it('emits improve event when Improve button clicked', () => {
    const fixture = create('writingScenario');
    let emitted = false;
    fixture.componentInstance.improve.subscribe(() => (emitted = true));
    fixture.nativeElement.querySelector('[data-testid="btn-improve"]').click();
    expect(emitted).toBeTrue();
  });

  it('emits tryAgain event when Try again button clicked', () => {
    const fixture = create('writingScenario');
    let emitted = false;
    fixture.componentInstance.tryAgain.subscribe(() => (emitted = true));
    fixture.nativeElement.querySelector('[data-testid="btn-try-again"]').click();
    expect(emitted).toBeTrue();
  });

  it('emits nextActivity event when Next activity button clicked', () => {
    const fixture = create();
    let emitted = false;
    fixture.componentInstance.nextActivity.subscribe(() => (emitted = true));
    fixture.nativeElement.querySelector('[data-testid="btn-next-activity"]').click();
    expect(emitted).toBeTrue();
  });

  it('emits backToDashboard event when Back button clicked', () => {
    const fixture = create();
    let emitted = false;
    fixture.componentInstance.backToDashboard.subscribe(() => (emitted = true));
    fixture.nativeElement.querySelector('[data-testid="btn-back-dashboard"]').click();
    expect(emitted).toBeTrue();
  });
});
