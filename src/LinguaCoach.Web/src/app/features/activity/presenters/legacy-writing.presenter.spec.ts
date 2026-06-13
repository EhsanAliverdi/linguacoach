import { LegacyWritingPresenter } from './legacy-writing.presenter';
import { makeFeedback } from './test-helpers';

describe('LegacyWritingPresenter', () => {
  const presenter = new LegacyWritingPresenter();

  it('returns writing teach content', () => {
    const teach = presenter.teachContent();
    expect(teach.block).toBe('writingLearning');
    expect(teach.skillBadge.label).toBe('Writing');
    expect(teach.ctaAction).toBe('startWriting');
  });

  it('returns writing practice content', () => {
    const practice = presenter.practiceContent();
    expect(practice.block).toBe('writingPractice');
    expect(practice.skillBadge.label).toBe('Writing');
  });

  it('derives feedback layout from patternEvaluation', () => {
    expect(presenter.feedbackLayout(makeFeedback())).toBe('legacy');
    expect(presenter.feedbackLayout(makeFeedback({ patternEvaluation: {} as any }))).toBe('pattern');
  });
});
