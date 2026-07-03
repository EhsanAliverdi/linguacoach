import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OnboardingV2QuestionStepComponent } from './onboarding-v2-question.component';
import { OnboardingV2Step } from '../../../../../core/models/onboarding-v2.models';

describe('OnboardingV2QuestionStepComponent', () => {
  let fixture: ComponentFixture<OnboardingV2QuestionStepComponent>;
  let component: OnboardingV2QuestionStepComponent;

  function setup(step: OnboardingV2Step): void {
    TestBed.configureTestingModule({ imports: [OnboardingV2QuestionStepComponent] });
    fixture = TestBed.createComponent(OnboardingV2QuestionStepComponent);
    component = fixture.componentInstance;
    component.step = step;
    fixture.detectChanges();
  }

  const singleChoiceStep: OnboardingV2Step = {
    stepKey: 'difficulty_preference',
    title: 'How challenging?',
    stepType: 'SingleChoice',
    requirementType: 'SystemRequired',
    stepOrder: 1,
    isEnabled: true,
    content: {
      type: 'single_choice', id: 'q1', questionText: 'How challenging?',
      choices: [{ key: 'Gentle', label: 'Gentle' }, { key: 'Balanced', label: 'Balanced' }],
    },
  };

  it('cannot submit until an answer is provided', () => {
    setup(singleChoiceStep);
    expect(component.canSubmit()).toBeFalse();
  });

  it('can submit once the shared renderer records an answer', () => {
    setup(singleChoiceStep);
    component.answers.set([{ questionId: 'q1', values: ['Balanced'] }]);
    expect(component.canSubmit()).toBeTrue();
  });

  it('submit emits the shared QuestionAnswer wire format', () => {
    setup(singleChoiceStep);
    component.answers.set([{ questionId: 'q1', values: ['Balanced'] }]);
    const spy = jasmine.createSpy();
    component.submitted.subscribe(spy);

    component.submit();

    expect(spy).toHaveBeenCalledWith(JSON.stringify({ answers: [{ questionId: 'q1', values: ['Balanced'] }] }));
  });

  it('allowSkip is true for AdminConfigured steps', () => {
    setup({ ...singleChoiceStep, requirementType: 'AdminConfigured' });
    expect(component.allowSkip()).toBeTrue();
  });

  it('allowSkip is false for SystemRequired steps', () => {
    setup(singleChoiceStep);
    expect(component.allowSkip()).toBeFalse();
  });

  it('skip emits an empty object regardless of answer state', () => {
    setup({ ...singleChoiceStep, requirementType: 'AdminConfigured' });
    const spy = jasmine.createSpy();
    component.submitted.subscribe(spy);

    component.skip();

    expect(spy).toHaveBeenCalledWith('{}');
  });

  it('resets answers when the step changes', () => {
    setup(singleChoiceStep);
    component.answers.set([{ questionId: 'q1', values: ['Balanced'] }]);

    component.ngOnChanges({
      step: { previousValue: singleChoiceStep, currentValue: { ...singleChoiceStep, stepKey: 'other' }, firstChange: false, isFirstChange: () => false },
    });

    expect(component.answers()).toEqual([]);
  });
});
