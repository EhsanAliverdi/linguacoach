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

const SCHEMA_JSON = JSON.stringify({
  display: 'form',
  components: [{ type: 'radio', key: 'q1', values: [{ label: 'was', value: 'A' }, { label: 'were', value: 'B' }] }],
});

const mockItem: AdaptivePlacementNextItem = {
  itemId: 'item-1',
  skill: 'grammar',
  targetCefrLevel: 'B1',
  itemType: 'multiple_choice',
  prompt: 'Choose the correct verb form.',
  itemOrder: 1,
  answeredCount: 0,
  estimatedRemainingItems: 9,
  formIoSchemaJson: SCHEMA_JSON,
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

  // ── parsedSchema / canSubmit ──────────────────────────────────────────────

  it('parsedSchema parses the item formIoSchemaJson', () => {
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.parsedSchema()).toEqual(JSON.parse(SCHEMA_JSON));
  });

  it('canSubmit is false without a valid schema, true once schema is present', () => {
    const noSchemaItem: AdaptivePlacementNextItem = { ...mockItem, formIoSchemaJson: null };
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(noSchemaItem),
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.canSubmit()).toBeFalse();
  });

  it('canSubmit is true once a schema is loaded and not submitting', () => {
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
    });
    fixture.detectChanges();
    expect(fixture.componentInstance.canSubmit()).toBeTrue();
  });

  // ── onFormSubmit() ────────────────────────────────────────────────────────

  it('onFormSubmit() sends the full submission data object', () => {
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(of({
      itemId: 'item-1', isCorrect: true, score: 1, evaluationNotes: '',
      assessmentComplete: true, completionReason: 'max_items', nextItem: null, summary: null,
    }));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
      respondToItem,
    });
    fixture.detectChanges();
    fixture.componentInstance.onFormSubmit({ q1: 'B' });

    expect(respondToItem).toHaveBeenCalledWith(jasmine.objectContaining({
      skill: 'grammar',
      submission: { data: { q1: 'B' } },
    }));
  });

  it('onFormSubmit() sends every component key from a multi-component submission', () => {
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(of({
      itemId: 'item-1', isCorrect: true, score: 1, evaluationNotes: '',
      assessmentComplete: true, completionReason: 'max_items', nextItem: null, summary: null,
    }));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
      respondToItem,
    });
    fixture.detectChanges();
    fixture.componentInstance.onFormSubmit({ q1: 'A', q2: 'C' });

    expect(respondToItem).toHaveBeenCalledWith(jasmine.objectContaining({
      submission: { data: { q1: 'A', q2: 'C' } },
    }));
  });

  it('onFormSubmit() returns to /placement when assessmentComplete', () => {
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
    fixture.componentInstance.onFormSubmit({ q1: 'A' });

    expect(router.navigate).toHaveBeenCalledWith(['/placement']);
  });

  it('onFormSubmit() returns to /placement when this skill card is done (no next item, not complete)', () => {
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
    fixture.componentInstance.onFormSubmit({ q1: 'B' });

    expect(router.navigate).toHaveBeenCalledWith(['/placement']);
  });

  it('onFormSubmit() loads next question when nextItem provided', () => {
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
    fixture.componentInstance.onFormSubmit({ q1: 'C' });

    expect(fixture.componentInstance.state()).toBe('question');
    expect(fixture.componentInstance.currentItem()?.itemId).toBe('item-2');
  });

  it('onFormSubmit() shows question with error on respondToItem failure', () => {
    const respondToItem = jasmine.createSpy('respondToItem').and.returnValue(
      throwError(() => ({ error: { error: 'Timeout' } })));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
      respondToItem,
    });
    fixture.detectChanges();
    fixture.componentInstance.onFormSubmit({ q1: 'A' });

    expect(fixture.componentInstance.state()).toBe('question');
    expect(fixture.componentInstance.error()).toBeTruthy();
  });

  // ── Listening audio ───────────────────────────────────────────────────────

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

    fixture.componentInstance.onFormSubmit({ q1: 'A' });

    expect(fixture.componentInstance.currentItem()?.itemId).toBe('item-2');
    expect(fixture.componentInstance.audioUrl()).toBeNull();
  });

  // ── placementContext (speakingResponse Form.io component upload bridge) ───

  it('placementContext is null before an item has loaded', () => {
    const { fixture } = setup({ getAdaptiveCurrent: () => of({ hasPlacement: false } as any) });
    expect(fixture.componentInstance.placementContext()).toBeNull();
  });

  it('placementContext.uploadSpeakingAudio delegates to PlacementService with the current assessment/item ids', async () => {
    const uploadAdaptiveSpeakingAudio = jasmine.createSpy('uploadAdaptiveSpeakingAudio')
      .and.returnValue(of({ storageKey: 'k', mimeType: 'audio/webm', durationSeconds: 3 }));
    const { fixture } = setup({
      getAdaptiveCurrent: () => of(makeSummary()),
      getAdaptiveNextItem: () => of(mockItem),
      uploadAdaptiveSpeakingAudio,
    });
    fixture.detectChanges();

    const ctx = fixture.componentInstance.placementContext();
    expect(ctx).not.toBeNull();
    const blob = new Blob(['x'], { type: 'audio/webm' });
    const result = await ctx!.uploadSpeakingAudio(blob, 'audio/webm', 2.5);

    expect(uploadAdaptiveSpeakingAudio).toHaveBeenCalledWith('assess-1', 'item-1', blob, 'audio/webm', 2.5);
    expect(result).toEqual({ storageKey: 'k', mimeType: 'audio/webm', durationSeconds: 3 });
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
