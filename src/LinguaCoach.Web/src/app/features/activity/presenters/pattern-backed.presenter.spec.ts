import { PatternBackedPresenter } from './pattern-backed.presenter';
import { makeActivity, makeFeedback } from './test-helpers';

describe('PatternBackedPresenter', () => {
  const presenter = new PatternBackedPresenter();

  it('renders a patternLearning teach block and an exerciseRenderer practice block', () => {
    const activity = makeActivity({ interactionMode: 'gapFill' });
    expect(presenter.teachContent(activity).block).toBe('patternLearning');
    expect(presenter.practiceContent(activity).block).toBe('exerciseRenderer');
  });

  it('extracts teaching-only fields from contentJson for the Learn stage', () => {
    const activity = makeActivity({
      interactionMode: 'gapFill',
      contentJson: JSON.stringify({
        title: 'Polite Requests',
        learningGoal: 'Use polite request phrases in workplace messages',
        instructions: 'Fill in each blank with the correct workplace word or phrase.',
        teachingNote: 'These phrases soften requests.',
        items: [{ sentence: 'Could you ___ this?', answer: 'review', distractors: ['look', 'see'] }],
      }),
    });
    const teach = presenter.teachContent(activity);
    expect(teach.block).toBe('patternLearning');
    if (teach.block === 'patternLearning') {
      expect(teach.title).toBe('Polite Requests');
      expect(teach.learningGoal).toBe('Use polite request phrases in workplace messages');
      expect(teach.instructions).toContain('Fill in each blank');
      expect(teach.teachingNote).toContain('soften requests');
      expect(teach.ctaLabel).toBe('Start practice');
      expect(teach.ctaAction).toBe('startPractice');
    }
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
