import { ActivityFeedbackDto } from '../../../core/models/activity.models';
import {
  ActivityPagePresenter, defaultFeedbackLayout, FeedbackLayout,
  PracticeViewModel, SkillBadge, TeachViewModel,
} from './activity-page-presenter';

const WRITING_BADGE: SkillBadge = { label: 'Writing', background: 'var(--sp-writing-soft)', color: 'var(--sp-writing-ink)', icon: 'writing' };

/** Bridges the legacy WritingScenario shape until it migrates to `open_writing_task`. */
export class LegacyWritingPresenter implements ActivityPagePresenter {
  teachContent(activity: import('../../../core/models/activity.models').ActivityDto): TeachViewModel {
    if (activity.stageContent?.learn) {
      return {
        block: 'stagedLearning',
        skillBadge: WRITING_BADGE,
        ctaLabel: 'Start practice',
        ctaAction: 'startWriting',
        learn: activity.stageContent.learn,
      };
    }

    return {
      block: 'writingLearning',
      skillBadge: WRITING_BADGE,
      ctaLabel: 'Start writing →',
      ctaAction: 'startWriting',
    };
  }

  practiceContent(): PracticeViewModel {
    return {
      block: 'writingPractice',
      skillBadge: WRITING_BADGE,
    };
  }

  feedbackLayout(feedback: ActivityFeedbackDto): FeedbackLayout {
    return defaultFeedbackLayout(feedback);
  }
}
