import { LegacyListeningPresenter } from './legacy-listening.presenter';
import { makeFeedback } from './test-helpers';

describe('LegacyListeningPresenter', () => {
  const presenter = new LegacyListeningPresenter();

  it('returns listening teach content', () => {
    const teach = presenter.teachContent();
    expect(teach.block).toBe('listeningLearning');
    expect(teach.skillBadge.label).toBe('Listening');
    expect(teach.ctaAction).toBe('startPractice');
  });

  it('returns listening practice content', () => {
    const practice = presenter.practiceContent();
    expect(practice.block).toBe('listeningPractice');
    expect(practice.skillBadge.label).toBe('Listening');
  });

  it('derives feedback layout from patternEvaluation', () => {
    expect(presenter.feedbackLayout(makeFeedback())).toBe('legacy');
    expect(presenter.feedbackLayout(makeFeedback({ patternEvaluation: {} as any }))).toBe('pattern');
  });
});
