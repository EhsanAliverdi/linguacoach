import { PatternBackedPresenter } from './pattern-backed.presenter';
import { makeActivity, makeFeedback, makeStageContent } from './test-helpers';

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

  it('moves exercise-setup fields (scenario, phrases, vocab, example) to the Practice stage, not Learn', () => {
    const activity = makeActivity({
      interactionMode: 'gapFill',
      contentJson: JSON.stringify({
        learningGoal: 'Use polite request phrases in workplace messages',
        scenario: 'Your manager asks you to review a report by Friday.',
        targetPhrases: ['Would you mind...', 'Could you possibly...'],
        targetVocabulary: ['deadline', 'review'],
        exampleText: 'Could you possibly review this by Friday?',
        commonMistakeToAvoid: 'Avoid "Give me the report".',
      }),
    });
    const teach = presenter.teachContent(activity);
    expect(teach.block).toBe('patternLearning');
    if (teach.block === 'patternLearning') {
      expect(teach.learningGoal).toBe('Use polite request phrases in workplace messages');
      expect((teach as any).scenario).toBeUndefined();
      expect((teach as any).targetPhrases).toBeUndefined();
      expect((teach as any).exampleText).toBeUndefined();
    }

    const practice = presenter.practiceContent(activity);
    expect(practice.scenario).toBe('Your manager asks you to review a report by Friday.');
    expect(practice.targetPhrases).toEqual(['Would you mind...', 'Could you possibly...']);
    expect(practice.targetVocabulary).toEqual(['deadline', 'review']);
    expect(practice.exampleText).toBe('Could you possibly review this by Friday?');
    expect(practice.commonMistakeToAvoid).toContain('Give me the report');
  });

  it('labels vocabulary interaction modes', () => {
    expect(presenter.practiceContent(makeActivity({ interactionMode: 'matchingPairs' })).skillBadge.label).toBe('Vocabulary');
    expect(presenter.practiceContent(makeActivity({ interactionMode: 'gapFill' })).skillBadge.label).toBe('Vocabulary');
  });

  it('labels listening interaction modes', () => {
    expect(presenter.practiceContent(makeActivity({ interactionMode: 'audioAndFreeText' })).skillBadge.label).toBe('Listening');
    expect(presenter.practiceContent(makeActivity({ interactionMode: 'audioAndGapFill' })).skillBadge.label).toBe('Listening');
    expect(presenter.practiceContent(makeActivity({ interactionMode: 'listeningFillInBlanks' })).skillBadge.label).toBe('Listening');
    expect(presenter.practiceContent(makeActivity({ interactionMode: 'highlightCorrectSummary' })).skillBadge.label).toBe('Listening');
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

  // ── staged module_stage_v1 activities ─────────────────────────────────────

  it('returns stagedLearning block when stageContent is present', () => {
    const activity = makeActivity({
      interactionMode: 'matchingPairs',
      exercisePatternKey: 'phrase_match',
      stageContent: makeStageContent(),
    });
    const teach = presenter.teachContent(activity);
    expect(teach.block).toBe('stagedLearning');
  });

  it('stagedLearning block carries the learn content from stageContent', () => {
    const activity = makeActivity({
      interactionMode: 'matchingPairs',
      exercisePatternKey: 'phrase_match',
      stageContent: makeStageContent({
        learn: {
          teachingTitle: 'Workplace Phrases',
          explanation: 'These phrases are used at work.',
          keyPoints: ['Notice the context'],
          examples: [{ phrase: 'action item', meaning: 'a task', note: 'meeting term' }],
          strategy: 'Look for the most natural meaning.',
          commonMistakes: ['Confusing register'],
          sourceLanguageSupport: null,
        },
      }),
    });
    const teach = presenter.teachContent(activity);
    expect(teach.block).toBe('stagedLearning');
    if (teach.block === 'stagedLearning') {
      expect(teach.learn.teachingTitle).toBe('Workplace Phrases');
      expect(teach.learn.explanation).toContain('at work');
      expect(teach.ctaLabel).toBe('Start practice');
      expect(teach.ctaAction).toBe('startPractice');
    }
  });

  it('stagedLearning does not carry answer controls or matching pairs', () => {
    const activity = makeActivity({
      interactionMode: 'matchingPairs',
      exercisePatternKey: 'phrase_match',
      stageContent: makeStageContent(),
    });
    const teach = presenter.teachContent(activity);
    expect((teach as any).pairs).toBeUndefined();
    expect((teach as any).exerciseData).toBeUndefined();
    expect((teach as any).submitLabel).toBeUndefined();
    expect((teach as any).checkLabel).toBeUndefined();
  });

  it('falls back to patternLearning block when stageContent is absent', () => {
    const activity = makeActivity({
      interactionMode: 'matchingPairs',
      exercisePatternKey: 'phrase_match',
      stageContent: null,
      contentJson: JSON.stringify({ title: 'Fallback', learningGoal: 'Legacy goal' }),
    });
    const teach = presenter.teachContent(activity);
    expect(teach.block).toBe('patternLearning');
  });

  it('practice block is exerciseRenderer for staged phrase_match', () => {
    const activity = makeActivity({
      interactionMode: 'matchingPairs',
      exercisePatternKey: 'phrase_match',
      stageContent: makeStageContent(),
    });
    expect(presenter.practiceContent(activity).block).toBe('exerciseRenderer');
  });

  it('practice block is exerciseRenderer for staged gap_fill_workplace_phrase', () => {
    const activity = makeActivity({
      interactionMode: 'gapFill',
      exercisePatternKey: 'gap_fill_workplace_phrase',
      stageContent: makeStageContent(),
    });
    expect(presenter.practiceContent(activity).block).toBe('exerciseRenderer');
  });
});
