import { LegacyVocabPresenter } from './legacy-vocab.presenter';
import { makeActivity, makeFeedback, makeStageContent } from './test-helpers';

describe('LegacyVocabPresenter', () => {
  const presenter = new LegacyVocabPresenter();

  it('returns vocab teach content', () => {
    const teach = presenter.teachContent(makeActivity({ activityType: 'vocabularyPractice', stageContent: null }));
    expect(teach.block).toBe('vocabLearning');
    expect(teach.skillBadge.label).toBe('Vocabulary');
    expect(teach.ctaAction).toBe('startPractice');
  });

  it('returns stagedLearning teach content when stageContent is present', () => {
    const stageContent = makeStageContent();
    const teach = presenter.teachContent(makeActivity({ activityType: 'vocabularyPractice', stageContent }));

    expect(teach.block).toBe('stagedLearning');
    if (teach.block !== 'stagedLearning') throw new Error('Expected stagedLearning');
    expect(teach.learn).toBe(stageContent.learn);
  });

  it('returns vocab practice content', () => {
    const practice = presenter.practiceContent();
    expect(practice.block).toBe('vocabPractice');
    expect(practice.skillBadge.label).toBe('Vocabulary');
  });

  it('derives feedback layout from patternEvaluation', () => {
    expect(presenter.feedbackLayout(makeFeedback())).toBe('legacy');
    expect(presenter.feedbackLayout(makeFeedback({ patternEvaluation: {} as any }))).toBe('pattern');
  });
});
