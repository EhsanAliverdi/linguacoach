import { ActivityDto, ActivityFeedbackDto } from '../../../core/models/activity.models';
import {
  ActivityPagePresenter, defaultFeedbackLayout, FeedbackLayout,
  PracticeViewModel, SkillBadge, TeachViewModel,
} from './activity-page-presenter';
import { parsePatternContent, stringArray, stringValue } from './pattern-content.util';

/**
 * Generic, data-driven presenter for any activity backed by the Exercise
 * Pattern Engine (`activity.interactionMode` set, or a migrated
 * `exercisePatternKey`). Works for all current and future pattern keys
 * without per-pattern presenter classes.
 */
export class PatternBackedPresenter implements ActivityPagePresenter {
  teachContent(activity: ActivityDto): TeachViewModel {
    const raw = parsePatternContent(activity);
    return {
      block: 'patternLearning',
      skillBadge: this.skillBadge(activity),
      ctaLabel: 'Start practice',
      ctaAction: 'startPractice',
      title: stringValue(raw, 'title') ?? activity.title,
      learningGoal: stringValue(raw, 'learningGoal'),
      instructions: stringValue(raw, 'instructions'),
      teachingNote: stringValue(raw, 'teachingNote'),
      skillFocus: stringValue(raw, 'skillFocus'),
      instructionInSourceLanguage: stringValue(raw, 'instructionInSourceLanguage'),
      toneGuidance: stringValue(raw, 'toneGuidance'),
    };
  }

  practiceContent(activity: ActivityDto): PracticeViewModel {
    const raw = parsePatternContent(activity);
    return {
      block: 'exerciseRenderer',
      skillBadge: this.skillBadge(activity),
      scenario: stringValue(raw, 'scenario'),
      situation: stringValue(raw, 'situation'),
      audience: stringValue(raw, 'audience'),
      tone: stringValue(raw, 'tone'),
      targetPhrases: stringArray(raw, 'targetPhrases'),
      targetVocabulary: stringArray(raw, 'targetVocabulary'),
      exampleText: stringValue(raw, 'exampleText') ?? stringValue(raw, 'exampleReply'),
      commonMistakeToAvoid: stringValue(raw, 'commonMistakeToAvoid'),
      speakerRole: stringValue(raw, 'speakerRole'),
      listenerRole: stringValue(raw, 'listenerRole'),
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
