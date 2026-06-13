import { PatternBackedPresenter } from './pattern-backed.presenter';
import { makeActivity, makeFeedback } from './test-helpers';

describe('PatternBackedPresenter', () => {
  const presenter = new PatternBackedPresenter();

  it('always renders the exercise renderer block', () => {
    const activity = makeActivity({ interactionMode: 'gapFill' });
    expect(presenter.teachContent(activity).block).toBe('exerciseRenderer');
    expect(presenter.practiceContent(activity).block).toBe('exerciseRenderer');
  });

  it('labels vocabulary interaction modes', () => {
    expect(presenter.practiceContent(makeActivity({ interactionMode: 'matchingPairs' })).skillBadge.label).toBe('Vocabulary');
    expect(presenter.practiceContent(makeActivity({ interactionMode: 'gapFill' })).skillBadge.label).toBe('Vocabulary');
  });

  it('labels listening interaction modes', () => {
    expect(presenter.practiceContent(makeActivity({ interactionMode: 'audioAndFreeText' })).skillBadge.label).toBe('Listening');
    expect(presenter.practiceContent(makeActivity({ interactionMode: 'audioAndGapFill' })).skillBadge.label).toBe('Listening');
  });

  it('labels readOnly as Reflection', () => {
    expect(presenter.practiceContent(makeActivity({ interactionMode: 'readOnly' })).skillBadge.label).toBe('Reflection');
  });

  it('labels chatReply on speakingRolePlay as Speaking', () => {
    const activity = makeActivity({ interactionMode: 'chatReply', activityType: 'speakingRolePlay' });
    expect(presenter.practiceContent(activity).skillBadge.label).toBe('Speaking');
  });

  it('labels chatReply on non-speaking activities as Writing', () => {
    const activity = makeActivity({ interactionMode: 'chatReply', activityType: 'writingScenario' });
    expect(presenter.practiceContent(activity).skillBadge.label).toBe('Writing');
  });

  it('labels freeTextEntry as Writing', () => {
    expect(presenter.practiceContent(makeActivity({ interactionMode: 'freeTextEntry' })).skillBadge.label).toBe('Writing');
  });

  it('derives feedback layout from patternEvaluation', () => {
    expect(presenter.feedbackLayout(makeFeedback())).toBe('legacy');
    expect(presenter.feedbackLayout(makeFeedback({ patternEvaluation: {} as any }))).toBe('pattern');
  });
});
