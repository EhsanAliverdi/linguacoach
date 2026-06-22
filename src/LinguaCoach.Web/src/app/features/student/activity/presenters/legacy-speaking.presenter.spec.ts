import { LegacySpeakingPresenter } from './legacy-speaking.presenter';
import { makeActivity, makeFeedback, makeStageContent } from './test-helpers';

describe('LegacySpeakingPresenter', () => {
  const presenter = new LegacySpeakingPresenter();

  describe('teachContent — legacy (no stageContent)', () => {
    it('returns speakingScenario block', () => {
      const teach = presenter.teachContent(makeActivity({ activityType: 'speakingRolePlay' }));
      expect(teach.block).toBe('speakingScenario');
      expect(teach.skillBadge.label).toBe('Speaking');
      expect(teach.ctaAction).toBe('startPractice');
    });

    it('returns startRecording as ctaLabel for legacy path', () => {
      const teach = presenter.teachContent(makeActivity({ activityType: 'speakingRolePlay', stageContent: null }));
      expect(teach.ctaLabel).toBe('Start recording');
    });
  });

  describe('teachContent — staged (stageContent present)', () => {
    it('returns stagedLearning block when stageContent.learn is present', () => {
      const stageContent = makeStageContent({
        learn: {
          teachingTitle: 'Roleplay preparation',
          explanation: 'A professional speaking strategy for workplace conversations.',
          keyPoints: ['State your purpose clearly.', 'Match the tone to the listener.'],
          examples: [{ phrase: 'I wanted to update you', meaning: 'Opens a status update', note: 'polite' }],
          strategy: 'Decide your opening sentence before recording.',
          commonMistakes: ['Too much background.'],
          sourceLanguageSupport: null,
        },
      });
      const teach = presenter.teachContent(makeActivity({ activityType: 'speakingRolePlay', stageContent }));
      expect(teach.block).toBe('stagedLearning');
      expect(teach.skillBadge.label).toBe('Speaking');
      expect(teach.ctaLabel).toBe('Start practice');
      expect(teach.ctaAction).toBe('startPractice');
    });

    it('learn VM comes exactly from stageContent.learn', () => {
      const learn = {
        teachingTitle: 'Speaking clearly in meetings',
        explanation: 'How to speak clearly in workplace meetings.',
        keyPoints: ['Use direct sentences.'],
        examples: [],
        strategy: 'Plan what to say first.',
        commonMistakes: [],
        sourceLanguageSupport: null,
      };
      const stageContent = makeStageContent({ learn });
      const teach = presenter.teachContent(makeActivity({ activityType: 'speakingRolePlay', stageContent }));
      if (teach.block !== 'stagedLearning') throw new Error('Expected stagedLearning');
      expect(teach.learn).toEqual(learn);
    });

    it('does not expose recording controls in stagedLearning block', () => {
      const stageContent = makeStageContent();
      const teach = presenter.teachContent(makeActivity({ activityType: 'speakingRolePlay', stageContent }));
      expect(teach.block).toBe('stagedLearning');
      expect(JSON.stringify(teach)).not.toContain('recordingControls');
      expect(JSON.stringify(teach)).not.toContain('startRecording');
      expect(JSON.stringify(teach)).not.toContain('microphone');
    });
  });

  describe('practiceContent', () => {
    it('returns speakingRecord block', () => {
      const practice = presenter.practiceContent();
      expect(practice.block).toBe('speakingRecord');
      expect(practice.skillBadge.label).toBe('Speaking');
    });
  });

  describe('feedbackLayout', () => {
    it('derives feedback layout from patternEvaluation', () => {
      expect(presenter.feedbackLayout(makeFeedback())).toBe('legacy');
      expect(presenter.feedbackLayout(makeFeedback({ patternEvaluation: {} as any }))).toBe('pattern');
    });
  });
});
