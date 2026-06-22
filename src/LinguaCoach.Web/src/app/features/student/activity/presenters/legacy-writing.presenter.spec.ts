import { LegacyWritingPresenter } from './legacy-writing.presenter';
import { makeActivity, makeFeedback, makeStageContent } from './test-helpers';

describe('LegacyWritingPresenter', () => {
  const presenter = new LegacyWritingPresenter();

  it('returns writing teach content', () => {
    const teach = presenter.teachContent(makeActivity({ stageContent: null }));
    expect(teach.block).toBe('writingLearning');
    expect(teach.skillBadge.label).toBe('Writing');
    expect(teach.ctaAction).toBe('startWriting');
  });

  it('returns stagedLearning teach content when stageContent is present', () => {
    const stageContent = makeStageContent();
    const activity = makeActivity({ stageContent });
    const teach = presenter.teachContent(activity);
    expect(teach.block).toBe('stagedLearning');
    expect(teach.skillBadge.label).toBe('Writing');
    expect(teach.ctaAction).toBe('startWriting');
    expect((teach as any).learn).toBe(stageContent.learn);
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
