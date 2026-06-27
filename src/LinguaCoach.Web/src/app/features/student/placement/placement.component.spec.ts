import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { PlacementComponent } from './placement.component';
import { PlacementService } from '../../../core/services/placement.service';
import {
  AdaptivePlacementSummary,
  AdaptivePlacementNextItem,
  PlacementConfig,
} from '../../../core/models/placement.models';

const defaultConfig: PlacementConfig = {
  placementRequiredBeforeLearning: true,
  allowSkipPlacement: false,
  allowPlacementRetake: false,
  autoStartPlacement: false,
};

function makeSummary(partial: Partial<AdaptivePlacementSummary> = {}): AdaptivePlacementSummary {
  return {
    assessmentId: 'assess-1',
    studentProfileId: 'profile-1',
    status: 'InProgress',
    startedAtUtc: '2026-01-01T00:00:00Z',
    completedAtUtc: null as any,
    expiredAtUtc: null as any,
    overallCefrLevel: null as any,
    overallConfidence: 0,
    isProvisional: false,
    resultSummary: null as any,
    source: 'adaptive',
    skillResults: [],
    learningPlanRegenerated: false,
    learningPlanRegenerationWarning: null as any,
    itemCount: 0,
    hasPlacement: true,
    ...partial,
  };
}

const mockItem: AdaptivePlacementNextItem = {
  itemId: 'item-1',
  skill: 'grammar',
  targetCefrLevel: 'B1',
  itemType: 'multiple_choice',
  prompt: 'Choose the correct verb form. (A) was (B) were (C) is',
  itemOrder: 1,
  answeredCount: 0,
  estimatedRemainingItems: 9,
};

describe('PlacementComponent', () => {
  function setup(overrides: Partial<PlacementService> = {}) {
    const svc: Partial<PlacementService> = {
      getPlacementConfig: () => of(defaultConfig),
      getAdaptiveCurrent: () => of({ hasPlacement: false } as any),
      startAdaptive: jasmine.createSpy('startAdaptive').and.returnValue(of(makeSummary())),
      resumeAdaptive: jasmine.createSpy('resumeAdaptive').and.returnValue(of(makeSummary())),
      getAdaptiveNextItem: jasmine.createSpy('getAdaptiveNextItem').and.returnValue(of(mockItem)),
      respondToItem: jasmine.createSpy('respondToItem'),
      completeAdaptive: jasmine.createSpy('completeAdaptive'),
      ...overrides,
    };

    TestBed.configureTestingModule({
      imports: [PlacementComponent],
      providers: [
        { provide: PlacementService, useValue: svc },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
      ],
    });

    const fixture = TestBed.createComponent(PlacementComponent);
    return { fixture, svc: TestBed.inject(PlacementService) as any, router: TestBed.inject(Router) as any };
  }

  // ── Init: no assessment ───────────────────────────────────────────────────

  it('shows welcome when no assessment exists', () => {
    const { fixture } = setup({
      getAdaptiveCurrent: () => of({ hasPlacement: false } as any),
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('welcome');
  });

  it('shows welcome when getAdaptiveCurrent returns null', () => {
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(null as any),
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('welcome');
  });

  it('shows welcome on getAdaptiveCurrent error', () => {
    const { fixture } = setup({
      getAdaptiveCurrent: () => throwError(() => new Error('network')),
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('welcome');
  });

  // ── Init: existing assessment ─────────────────────────────────────────────

  it('transitions to question when existing InProgress assessment', () => {
    const inProgress = makeSummary({ status: 'InProgress' });
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(inProgress),
      getAdaptiveNextItem: () => of(mockItem),
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('question');
    expect(fixture.componentInstance.currentItem()?.itemId).toBe('item-1');
  });

  it('shows done state when existing assessment is Completed', () => {
    const completed = makeSummary({ status: 'Completed', overallCefrLevel: 'B1' });
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(completed),
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('done');
    expect(fixture.componentInstance.assessment()?.overallCefrLevel).toBe('B1');
  });

  it('shows welcome for Abandoned assessment', () => {
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary({ status: 'Abandoned' })),
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('welcome');
  });

  // ── begin() ───────────────────────────────────────────────────────────────

  it('begin() calls startAdaptive and loads first question', () => {
    const inProgress = makeSummary({ status: 'InProgress' });
    const startAdaptive = jasmine.createSpy('startAdaptive').and.returnValue(of(inProgress));
    const getAdaptiveNextItem = jasmine.createSpy('getAdaptiveNextItem').and.returnValue(of(mockItem));
    const { fixture } = setup({ startAdaptive, getAdaptiveNextItem });

    fixture.detectChanges();
    fixture.componentInstance.begin();

    expect(startAdaptive).toHaveBeenCalled();
    expect(getAdaptiveNextItem).toHaveBeenCalledWith('assess-1');
    expect(fixture.componentInstance.state()).toBe('question');
  });

  it('begin() calls resumeAdaptive when autoStartPlacement is true', () => {
    const autoConfig = { ...defaultConfig, autoStartPlacement: true };
    const inProgress = makeSummary({ status: 'InProgress' });
    const resumeAdaptive = jasmine.createSpy('resumeAdaptive').and.returnValue(of(inProgress));
    const getAdaptiveNextItem = jasmine.createSpy('getAdaptiveNextItem').and.returnValue(of(mockItem));
    const { fixture } = setup({
      getPlacementConfig: () => of(autoConfig),
      resumeAdaptive,
      getAdaptiveNextItem,
    });

    fixture.detectChanges();
    fixture.componentInstance.begin();

    expect(resumeAdaptive).toHaveBeenCalled();
    expect(fixture.componentInstance.state()).toBe('question');
  });

  it('begin() transitions to error state on startAdaptive failure', () => {
    const { fixture } = setup({
      startAdaptive: () => throwError(() => ({ error: { error: 'Server error' } })),
    });
    fixture.detectChanges();
    fixture.componentInstance.begin();
    expect(fixture.componentInstance.state()).toBe('error');
    expect(fixture.componentInstance.error()).toBeTruthy();
  });

  // ── selectChoice() / canSubmit ────────────────────────────────────────────

  it('selectChoice() sets selectedAnswer and enables submit', () => {
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.canSubmit()).toBeFalse();
    fixture.componentInstance.selectChoice('A');
    expect(fixture.componentInstance.selectedAnswer()).toBe('A');
    expect(fixture.componentInstance.canSubmit()).toBeTrue();
  });

  // ── submitAnswer() ────────────────────────────────────────────────────────

  it('submitAnswer() transitions to done when assessmentComplete with summary', () => {
    const completedSummary = makeSummary({ status: 'Completed', overallCefrLevel: 'B2' });
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(of({
      itemId: 'item-1',
      isCorrect: true,
      score: 1,
      evaluationNotes: '',
      assessmentComplete: true,
      completionReason: 'max_items',
      nextItem: null,
      summary: completedSummary,
    }));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
      respondToItem,
    });
    fixture.detectChanges();
    fixture.componentInstance.selectChoice('A');
    fixture.componentInstance.submitAnswer();

    expect(respondToItem).toHaveBeenCalled();
    expect(fixture.componentInstance.state()).toBe('done');
    expect(fixture.componentInstance.assessment()?.overallCefrLevel).toBe('B2');
  });

  it('submitAnswer() triggers completion when assessmentComplete without summary', () => {
    const completedSummary = makeSummary({ status: 'Completed' });
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(of({
      itemId: 'item-1',
      isCorrect: true,
      score: 1,
      evaluationNotes: '',
      assessmentComplete: true,
      completionReason: 'max_items',
      nextItem: null,
      summary: null,
    }));
    const completeAdaptive = jasmine.createSpy('completeAdaptive').and.returnValue(of(completedSummary));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
      respondToItem,
      completeAdaptive,
    });
    fixture.detectChanges();
    fixture.componentInstance.selectChoice('B');
    fixture.componentInstance.submitAnswer();

    expect(completeAdaptive).toHaveBeenCalledWith('assess-1');
    expect(fixture.componentInstance.state()).toBe('done');
  });

  it('submitAnswer() loads next question when nextItem provided', () => {
    const nextItem: AdaptivePlacementNextItem = { ...mockItem, itemId: 'item-2', answeredCount: 1 };
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(of({
      itemId: 'item-1',
      isCorrect: false,
      score: 0,
      evaluationNotes: '',
      assessmentComplete: false,
      completionReason: null,
      nextItem,
      summary: null,
    }));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
      respondToItem,
    });
    fixture.detectChanges();
    fixture.componentInstance.selectChoice('C');
    fixture.componentInstance.submitAnswer();

    expect(fixture.componentInstance.state()).toBe('question');
    expect(fixture.componentInstance.currentItem()?.itemId).toBe('item-2');
  });

  it('submitAnswer() shows question with error on respondToItem failure', () => {
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(
      throwError(() => ({ error: { error: 'Timeout' } })));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
      respondToItem,
    });
    fixture.detectChanges();
    fixture.componentInstance.selectChoice('A');
    fixture.componentInstance.submitAnswer();

    expect(fixture.componentInstance.state()).toBe('question');
    expect(fixture.componentInstance.error()).toBeTruthy();
  });

  // ── continueToDashboard() ─────────────────────────────────────────────────

  it('continueToDashboard() navigates to /dashboard', () => {
    const { fixture, router } = setup();
    fixture.detectChanges();
    fixture.componentInstance.continueToDashboard();
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  // ── parseQuestionText() ───────────────────────────────────────────────────

  it('parseQuestionText() strips choices block', () => {
    const comp = TestBed.runInInjectionContext(() =>
      new PlacementComponent(
        { getPlacementConfig: () => of(defaultConfig), getAdaptiveCurrent: () => of(null as any) } as any,
        { navigate: jasmine.createSpy() } as any,
      ));
    expect(comp.parseQuestionText('Fill in the blank. (A) is (B) are')).toBe('Fill in the blank.');
    expect(comp.parseQuestionText('What is this?')).toBe('What is this?');
    expect(comp.parseQuestionText('')).toBe('');
  });

  // ── parseChoices() ────────────────────────────────────────────────────────

  it('parseChoices() extracts A/B/C choices', () => {
    const comp = TestBed.runInInjectionContext(() =>
      new PlacementComponent(
        { getPlacementConfig: () => of(defaultConfig), getAdaptiveCurrent: () => of(null as any) } as any,
        { navigate: jasmine.createSpy() } as any,
      ));
    const choices = comp.parseChoices('Q text (A) First option (B) Second option (C) Third option');
    expect(choices.length).toBe(3);
    expect(choices[0]).toEqual({ letter: 'A', text: 'First option' });
    expect(choices[1]).toEqual({ letter: 'B', text: 'Second option' });
    expect(choices[2]).toEqual({ letter: 'C', text: 'Third option' });
  });

  it('parseChoices() returns empty for gap_fill prompts', () => {
    const comp = TestBed.runInInjectionContext(() =>
      new PlacementComponent(
        { getPlacementConfig: () => of(defaultConfig), getAdaptiveCurrent: () => of(null as any) } as any,
        { navigate: jasmine.createSpy() } as any,
      ));
    expect(comp.parseChoices('Complete the sentence: She ___ to school.')).toEqual([]);
  });

  // ── skillLabel() ──────────────────────────────────────────────────────────

  it('skillLabel() capitalises first letter', () => {
    const comp = TestBed.runInInjectionContext(() =>
      new PlacementComponent(
        { getPlacementConfig: () => of(defaultConfig), getAdaptiveCurrent: () => of(null as any) } as any,
        { navigate: jasmine.createSpy() } as any,
      ));
    expect(comp.skillLabel('grammar')).toBe('Grammar');
    expect(comp.skillLabel('vocabulary')).toBe('Vocabulary');
    expect(comp.skillLabel('')).toBe('');
    expect(comp.skillLabel(null as any)).toBe('');
  });
});
