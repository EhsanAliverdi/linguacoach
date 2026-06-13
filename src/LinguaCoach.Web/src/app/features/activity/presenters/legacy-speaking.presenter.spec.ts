import { LegacySpeakingPresenter } from './legacy-speaking.presenter';
import { makeFeedback } from './test-helpers';

describe('LegacySpeakingPresenter', () => {
  const presenter = new LegacySpeakingPresenter();

  it('returns speaking teach content', () => {
    const teach = presenter.teachContent();
    expect(teach.block).toBe('speakingScenario');
    expect(teach.skillBadge.label).toBe('Speaking');
    expect(teach.ctaAction).toBe('startPractice');
  });

  it('returns speaking practice content', () => {
    const practice = presenter.practiceContent();
    expect(practice.block).toBe('speakingRecord');
    expect(practice.skillBadge.label).toBe('Speaking');
  });

  it('derives feedback layout from patternEvaluation', () => {
    expect(presenter.feedbackLayout(makeFeedback())).toBe('legacy');
    expect(presenter.feedbackLayout(makeFeedback({ patternEvaluation: {} as any }))).toBe('pattern');
  });
});
