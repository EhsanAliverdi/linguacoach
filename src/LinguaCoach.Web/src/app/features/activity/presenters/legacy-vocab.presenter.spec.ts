import { LegacyVocabPresenter } from './legacy-vocab.presenter';
import { makeFeedback } from './test-helpers';

describe('LegacyVocabPresenter', () => {
  const presenter = new LegacyVocabPresenter();

  it('returns vocab teach content', () => {
    const teach = presenter.teachContent();
    expect(teach.block).toBe('vocabLearning');
    expect(teach.skillBadge.label).toBe('Vocabulary');
    expect(teach.ctaAction).toBe('startPractice');
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
