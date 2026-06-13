import { ActivityFeedbackDto } from '../../../core/models/activity.models';
import {
  ActivityPagePresenter, defaultFeedbackLayout, FeedbackLayout,
  PracticeViewModel, TeachViewModel,
} from './activity-page-presenter';

const VOCAB_BADGE = { label: 'Vocabulary', background: '#ede9fe', color: '#6d28d9', icon: 'vocab' as const };

/** Bridges the legacy VocabularyPractice shape until it migrates to `vocabulary_practice`. */
export class LegacyVocabPresenter implements ActivityPagePresenter {
  teachContent(): TeachViewModel {
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
