import { ActivityDto, ActivityFeedbackDto } from '../../../core/models/activity.models';
import {
  ActivityPagePresenter, defaultFeedbackLayout, FeedbackLayout,
  PracticeViewModel, SkillBadge, TeachViewModel,
} from './activity-page-presenter';

const LISTENING_BADGE: SkillBadge = { label: 'Listening', background: '#e0f2fe', color: '#0369a1', icon: 'listening' };

/** Bridges the legacy ListeningComprehension shape until it migrates to `listening_comprehension`. */
export class LegacyListeningPresenter implements ActivityPagePresenter {
  teachContent(activity: ActivityDto): TeachViewModel {
    if (activity.stageContent) {
      return {
        block: 'stagedLearning',
        skillBadge: LISTENING_BADGE,
        ctaLabel: 'Start practice',
        ctaAction: 'startPractice',
        learn: activity.stageContent.learn,
      };
    }
    // Defensive fallback only — backend adapter always populates stageContent for ListeningComprehension.
    return {
      block: 'listeningLearning',
      skillBadge: LISTENING_BADGE,
      ctaLabel: 'Answer questions',
      ctaAction: 'startPractice',
    };
  }

  practiceContent(): PracticeViewModel {
    return {
      block: 'listeningPractice',
      skillBadge: LISTENING_BADGE,
    };
  }

  feedbackLayout(feedback: ActivityFeedbackDto): FeedbackLayout {
    return defaultFeedbackLayout(feedback);
  }
}
