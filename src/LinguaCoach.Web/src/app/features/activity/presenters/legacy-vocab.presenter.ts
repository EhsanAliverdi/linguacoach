import { ActivityDto, ActivityFeedbackDto } from '../../../core/models/activity.models';
import {
  ActivityPagePresenter, defaultFeedbackLayout, FeedbackLayout,
  PracticeViewModel, TeachViewModel,
} from './activity-page-presenter';

const VOCAB_BADGE = { label: 'Vocabulary', background: '#ede9fe', color: '#6d28d9', icon: 'vocab' as const };

/** Bridges VocabularyPractice while preserving the legacy fallback path. */
export class LegacyVocabPresenter implements ActivityPagePresenter {
  teachContent(activity: ActivityDto): TeachViewModel {
    if (activity.stageContent?.learn) {
      return {
        block: 'stagedLearning',
        skillBadge: VOCAB_BADGE,
        ctaLabel: 'Start practice',
        ctaAction: 'startPractice',
        learn: activity.stageContent.learn,
      };
    }

    return {
      block: 'vocabLearning',
      skillBadge: VOCAB_BADGE,
      ctaLabel: 'Start practice',
      ctaAction: 'startPractice',
    };
  }

  practiceContent(): PracticeViewModel {
    return {
      block: 'vocabPractice',
      skillBadge: VOCAB_BADGE,
    };
  }

  feedbackLayout(feedback: ActivityFeedbackDto): FeedbackLayout {
    return defaultFeedbackLayout(feedback);
  }
}
