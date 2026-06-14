import { LegacyListeningPresenter } from './legacy-listening.presenter';
import { makeActivity, makeFeedback, makeStageContent } from './test-helpers';

describe('LegacyListeningPresenter', () => {
  const presenter = new LegacyListeningPresenter();

  it('returns stagedLearning teach content when stageContent is present', () => {
    const stageContent = makeStageContent();
    const activity = makeActivity({ stageContent });
    const teach = presenter.teachContent(activity);
    expect(teach.block).toBe('stagedLearning');
    expect(teach.skillBadge.label).toBe('Listening');
    expect(teach.ctaAction).toBe('startPractice');
    expect((teach as any).learn).toBe(stageContent.learn);
  });

  it('falls back to listeningLearning when stageContent is missing', () => {
    const activity = makeActivity({ stageContent: null });
    const teach = presenter.teachContent(activity);
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
