import { ActivityDto, ActivityFeedbackDto } from '../../../../core/models/activity.models';
import {
  ActivityPagePresenter, defaultFeedbackLayout, FeedbackLayout,
  PracticeViewModel, SkillBadge, TeachViewModel,
} from './activity-page-presenter';

const SPEAKING_BADGE: SkillBadge = { label: 'Speaking', background: '#fef3c7', color: '#92400e', icon: 'speaking' as const };

/** Bridges the legacy SpeakingRolePlay shape and staged module_stage_v1 content. */
export class LegacySpeakingPresenter implements ActivityPagePresenter {
  teachContent(activity: ActivityDto): TeachViewModel {
    if (activity.stageContent?.learn) {
      return {
        block: 'stagedLearning',
        skillBadge: SPEAKING_BADGE,
        ctaLabel: 'Start practice',
        ctaAction: 'startPractice',
        learn: activity.stageContent.learn,
      };
    }

    return {
      block: 'speakingScenario',
      skillBadge: SPEAKING_BADGE,
      ctaLabel: 'Start recording',
      ctaAction: 'startPractice',
    };
  }

  practiceContent(): PracticeViewModel {
    return {
      block: 'speakingRecord',
      skillBadge: SPEAKING_BADGE,
    };
  }

  feedbackLayout(feedback: ActivityFeedbackDto): FeedbackLayout {
    return defaultFeedbackLayout(feedback);
  }
}

