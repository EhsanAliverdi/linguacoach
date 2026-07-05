import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ActivatedRoute, Router } from '@angular/router';
import { PlacementComponent } from './placement.component';
import { PlacementService } from '../../../core/services/placement.service';
import { AdaptivePlacementSummary, AdaptivePlacementNextItem } from '../../../core/models/placement.models';

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
  function setup(overrides: Partial<PlacementService> = {}, skillParam: string | null = 'grammar') {
    const svc: Partial<PlacementService> = {
      getAdaptiveCurrent: () => of({ hasPlacement: false } as any),
      startAdaptive: jasmine.createSpy('startAdaptive').and.returnValue(of(makeSummary())),
      getAdaptiveNextItem: jasmine.createSpy('getAdaptiveNextItem').and.returnValue(of(mockItem)),
      respondToItem: jasmine.createSpy('respondToItem'),
      completeAdaptive: jasmine.createSpy('completeAdaptive'),
      getAdaptiveItemAudioBlobUrl: jasmine.createSpy('getAdaptiveItemAudioBlobUrl').and.returnValue(of('blob:fake-url')),
      ...overrides,
    };

    TestBed.configureTestingModule({
      imports: [PlacementComponent],
      providers: [
        { provide: PlacementService, useValue: svc },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: { get: () => skillParam } } },
        },
      ],
    });

    const fixture = TestBed.createComponent(PlacementComponent);
    return { fixture, svc: TestBed.inject(PlacementService) as any, router: TestBed.inject(Router) as any };
  }

  // ── Init ──────────────────────────────────────────────────────────────────

  it('redirects to /placement when no :skill route param is present', () => {
    const { fixture, router } = setup({}, null);
    fixture.detectChanges();
    expect(router.navigate).toHaveBeenCalledWith(['/placement']);
  });

  it('starts a new assessment when none exists, then loads the first scoped item', () => {
    const inProgress = makeSummary({ status: 'InProgress' });
    const startAdaptive = jasmine.createSpy('startAdaptive').and.returnValue(of(inProgress));
    const getAdaptiveNextItem = jasmine.createSpy('getAdaptiveNextItem').and.returnValue(of(mockItem));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of({ hasPlacement: false } as any),
      startAdaptive,
      getAdaptiveNextItem,
    });
    fixture.detectChanges();

    expect(startAdaptive).toHaveBeenCalled();
    expect(getAdaptiveNextItem).toHaveBeenCalledWith('assess-1', 'grammar');
    expect(fixture.componentInstance.state()).toBe('question');
  });

  it('redirects to /placement when the whole assessment is already Completed', () => {
    const { fixture, router } = setup({
      getAdaptiveCurrent: () => of(makeSummary({ status: 'Completed' })),
    });
    fixture.detectChanges();
    expect(router.navigate).toHaveBeenCalledWith(['/placement']);
  });

  it('redirects to /placement when this skill has no next item (card already done)', () => {
    const { fixture, router } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(null),
    });
    fixture.detectChanges();
    expect(router.navigate).toHaveBeenCalledWith(['/placement']);
  });

  it('loads the scoped item for an existing in-progress assessment', () => {
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.state()).toBe('question');
    expect(fixture.componentInstance.currentItem()?.itemId).toBe('item-1');
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

  it('submitAnswer() sends the skill and returns to /placement when assessmentComplete', () => {
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(of({
      itemId: 'item-1', isCorrect: true, score: 1, evaluationNotes: '',
      assessmentComplete: true, completionReason: 'max_items', nextItem: null, summary: null,
    }));
    const { fixture, router } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
      respondToItem,
    });
    fixture.detectChanges();
    fixture.componentInstance.selectChoice('A');
    fixture.componentInstance.submitAnswer();

    expect(respondToItem).toHaveBeenCalledWith(jasmine.objectContaining({ skill: 'grammar' }));
    expect(router.navigate).toHaveBeenCalledWith(['/placement']);
  });

  it('submitAnswer() returns to /placement when this skill card is done (no next item, not complete)', () => {
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(of({
      itemId: 'item-1', isCorrect: true, score: 1, evaluationNotes: '',
      assessmentComplete: false, completionReason: null, nextItem: null, summary: null,
    }));
    const { fixture, router } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
      respondToItem,
    });
    fixture.detectChanges();
    fixture.componentInstance.selectChoice('B');
    fixture.componentInstance.submitAnswer();

    expect(router.navigate).toHaveBeenCalledWith(['/placement']);
  });

  it('submitAnswer() loads next question when nextItem provided', () => {
    const nextItem: AdaptivePlacementNextItem = { ...mockItem, itemId: 'item-2', answeredCount: 1 };
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(of({
      itemId: 'item-1', isCorrect: false, score: 0, evaluationNotes: '',
      assessmentComplete: false, completionReason: null, nextItem, summary: null,
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

  // ── parseQuestionText() ───────────────────────────────────────────────────

  it('parseQuestionText() strips choices block', () => {
    const comp = TestBed.runInInjectionContext(() =>
      new PlacementComponent(
        { getAdaptiveCurrent: () => of(null as any) } as any,
        { navigate: jasmine.createSpy() } as any,
        { snapshot: { paramMap: { get: () => 'grammar' } } } as any,
      ));
    expect(comp.parseQuestionText('Fill in the blank. (A) is (B) are')).toBe('Fill in the blank.');
    expect(comp.parseQuestionText('What is this?')).toBe('What is this?');
    expect(comp.parseQuestionText('')).toBe('');
  });

  // ── parseChoices() ────────────────────────────────────────────────────────

  it('parseChoices() extracts A/B/C choices', () => {
    const comp = TestBed.runInInjectionContext(() =>
      new PlacementComponent(
        { getAdaptiveCurrent: () => of(null as any) } as any,
        { navigate: jasmine.createSpy() } as any,
        { snapshot: { paramMap: { get: () => 'grammar' } } } as any,
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
        { getAdaptiveCurrent: () => of(null as any) } as any,
        { navigate: jasmine.createSpy() } as any,
        { snapshot: { paramMap: { get: () => 'grammar' } } } as any,
      ));
    expect(comp.parseChoices('Complete the sentence: She ___ to school.')).toEqual([]);
  });

  // ── Listening audio (Phase 20I-5) ────────────────────────────────────────

  const listeningItem: AdaptivePlacementNextItem = {
    ...mockItem,
    itemId: 'item-listening',
    skill: 'listening',
    hasAudio: true,
  };

  it('loads audio blob URL when the item hasAudio', () => {
    const getAdaptiveItemAudioBlobUrl = jasmine.createSpy('getAdaptiveItemAudioBlobUrl').and.returnValue(of('blob:fake-url'));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(listeningItem),
      getAdaptiveItemAudioBlobUrl,
    });
    fixture.detectChanges();

    expect(getAdaptiveItemAudioBlobUrl).toHaveBeenCalledWith('assess-1', 'item-listening');
    expect(fixture.componentInstance.audioUrl()).toBe('blob:fake-url');
  });

  it('does not load audio when the item has no audio', () => {
    const getAdaptiveItemAudioBlobUrl = jasmine.createSpy('getAdaptiveItemAudioBlobUrl');
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
      getAdaptiveItemAudioBlobUrl,
    });
    fixture.detectChanges();

    expect(getAdaptiveItemAudioBlobUrl).not.toHaveBeenCalled();
    expect(fixture.componentInstance.audioUrl()).toBeNull();
  });

  it('audioUrl stays null when audio fails to load', () => {
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(listeningItem),
      getAdaptiveItemAudioBlobUrl: () => throwError(() => new Error('audio failed')),
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.audioUrl()).toBeNull();
    expect(fixture.componentInstance.state()).toBe('question');
  });

  it('resets audioUrl when moving to the next question', () => {
    const nextItem: AdaptivePlacementNextItem = { ...mockItem, itemId: 'item-2', hasAudio: false };
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(of({
      itemId: 'item-listening', isCorrect: true, score: 1, evaluationNotes: '',
      assessmentComplete: false, completionReason: null, nextItem, summary: null,
    }));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(listeningItem),
      respondToItem,
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.audioUrl()).toBe('blob:fake-url');

    fixture.componentInstance.selectChoice('A');
    fixture.componentInstance.submitAnswer();

    expect(fixture.componentInstance.currentItem()?.itemId).toBe('item-2');
    expect(fixture.componentInstance.audioUrl()).toBeNull();
  });

  // ── Unified Question-Schema (Phase 3): content-driven answers ───────────

  it('canSubmit is false until the structured content answer is complete', () => {
    const contentItem: AdaptivePlacementNextItem = {
      ...mockItem,
      content: {
        type: 'single_choice',
        id: 'q1',
        questionText: 'Choose the correct verb form.',
        choices: [{ key: 'A', label: 'was' }, { key: 'B', label: 'were' }],
      },
    };
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(contentItem),
    });
    fixture.detectChanges();

    expect(fixture.componentInstance.canSubmit()).toBeFalse();
    fixture.componentInstance.answers.set([{ questionId: 'q1', values: ['A'] }]);
    expect(fixture.componentInstance.canSubmit()).toBeTrue();
  });

  it('submitAnswer() sends the first leaf answer as the legacy response string', () => {
    const contentItem: AdaptivePlacementNextItem = {
      ...mockItem,
      content: {
        type: 'single_choice',
        id: 'q1',
        questionText: 'Choose the correct verb form.',
        choices: [{ key: 'A', label: 'was' }, { key: 'B', label: 'were' }],
      },
    };
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(of({
      itemId: 'item-1', isCorrect: true, score: 1, evaluationNotes: '',
      assessmentComplete: true, completionReason: 'max_items', nextItem: null, summary: null,
    }));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(contentItem),
      respondToItem,
    });
    fixture.detectChanges();
    fixture.componentInstance.answers.set([{ questionId: 'q1', values: ['B'] }]);
    fixture.componentInstance.submitAnswer();

    expect(respondToItem).toHaveBeenCalledWith(jasmine.objectContaining({ response: 'B' }));
  });

  it('resets structured answers when moving to the next question', () => {
    const contentItem: AdaptivePlacementNextItem = {
      ...mockItem,
      content: { type: 'gap_fill', id: 'q1', questionText: 'Complete the sentence.' },
    };
    const nextItem: AdaptivePlacementNextItem = { ...mockItem, itemId: 'item-2' };
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(of({
      itemId: 'item-1', isCorrect: true, score: 1, evaluationNotes: '',
      assessmentComplete: false, completionReason: null, nextItem, summary: null,
    }));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(contentItem),
      respondToItem,
    });
    fixture.detectChanges();
    fixture.componentInstance.answers.set([{ questionId: 'q1', values: ['answer'] }]);
    fixture.componentInstance.submitAnswer();

    expect(fixture.componentInstance.currentItem()?.itemId).toBe('item-2');
    expect(fixture.componentInstance.answers()).toEqual([]);
  });

  // ── skillLabel() ──────────────────────────────────────────────────────────

  it('skillLabel() capitalises first letter', () => {
    const comp = TestBed.runInInjectionContext(() =>
      new PlacementComponent(
        { getAdaptiveCurrent: () => of(null as any) } as any,
        { navigate: jasmine.createSpy() } as any,
        { snapshot: { paramMap: { get: () => 'grammar' } } } as any,
      ));
    expect(comp.skillLabel('grammar')).toBe('Grammar');
    expect(comp.skillLabel('vocabulary')).toBe('Vocabulary');
    expect(comp.skillLabel('')).toBe('');
    expect(comp.skillLabel(null as any)).toBe('');
  });
});
