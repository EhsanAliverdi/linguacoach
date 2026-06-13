import { ActivityDto, ActivityFeedbackDto } from '../../../core/models/activity.models';
import {
  ActivityPagePresenter, defaultFeedbackLayout, FeedbackLayout,
  PracticeViewModel, SkillBadge, TeachViewModel,
} from './activity-page-presenter';

/**
 * Generic, data-driven presenter for any activity backed by the Exercise
 * Pattern Engine (`activity.interactionMode` set, or a migrated
 * `exercisePatternKey`). Works for all current and future pattern keys
 * without per-pattern presenter classes.
 */
export class PatternBackedPresenter implements ActivityPagePresenter {
  teachContent(activity: ActivityDto): TeachViewModel {
    return {
      block: 'exerciseRenderer',
      skillBadge: this.skillBadge(activity),
      ctaLabel: '',
      ctaAction: 'startWriting',
    };
  }

  practiceContent(activity: ActivityDto): PracticeViewModel {
    return {
      block: 'exerciseRenderer',
      skillBadge: this.skillBadge(activity),
    };
  }

  feedbackLayout(feedback: ActivityFeedbackDto): FeedbackLayout {
    return defaultFeedbackLayout(feedback);
  }

  private skillBadge(activity: ActivityDto): SkillBadge {
    switch (activity.interactionMode) {
      case 'matchingPairs':
      case 'gapFill':
        return { label: 'Vocabulary', background: '#ede9fe', color: '#6d28d9', icon: 'vocab' };
      case 'audioAndFreeText':
      case 'audioAndGapFill':
        return { label: 'Listening', background: '#e0f2fe', color: '#0369a1', icon: 'listening' };
      case 'readOnly':
        return { label: 'Reflection', background: 'var(--sp-writing-soft)', color: 'var(--sp-writing-ink)', icon: 'writing' };
      case 'chatReply':
      case 'freeTextEntry':
      default:
        return activity.activityType === 'speakingRolePlay'
          ? { label: 'Speaking', background: '#fef3c7', color: '#92400e', icon: 'speaking' }
          : { label: 'Writing', background: 'var(--sp-writing-soft)', color: 'var(--sp-writing-ink)', icon: 'writing' };
    }
  }
}
