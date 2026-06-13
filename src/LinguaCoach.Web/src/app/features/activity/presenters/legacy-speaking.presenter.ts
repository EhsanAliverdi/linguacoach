import { ActivityFeedbackDto } from '../../../core/models/activity.models';
import {
  ActivityPagePresenter, defaultFeedbackLayout, FeedbackLayout,
  PracticeViewModel, TeachViewModel,
} from './activity-page-presenter';

const SPEAKING_BADGE = { label: 'Speaking', background: '#fef3c7', color: '#92400e', icon: 'speaking' as const };

/** Bridges the legacy SpeakingRolePlay shape until it migrates to `speaking_roleplay_turn`. */
export class LegacySpeakingPresenter implements ActivityPagePresenter {
  teachContent(): TeachViewModel {
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
