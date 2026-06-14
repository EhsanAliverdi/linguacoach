import { ActivityDto, ActivityFeedbackDto } from '../../../core/models/activity.models';

export type SkillIcon = 'speaking' | 'vocab' | 'listening' | 'writing';

export interface SkillBadge {
  label: string;
  background: string;
  color: string;
  icon: SkillIcon;
}

export type TeachBlock =
  | 'speakingScenario'
  | 'vocabLearning'
  | 'listeningLearning'
  | 'writingLearning'
  | 'patternLearning'
  | 'exerciseRenderer';

export interface PatternLearningViewModel {
  block: 'patternLearning';
  skillBadge: SkillBadge;
  ctaLabel: string;
  ctaAction: 'startPractice' | 'startWriting';
  title?: string;
  learningGoal?: string;
  instructions?: string;
  teachingNote?: string;
  scenario?: string;
  situation?: string;
  audience?: string;
  tone?: string;
  skillFocus?: string;
  targetPhrases?: string[];
  targetVocabulary?: string[];
  exampleText?: string;
  commonMistakeToAvoid?: string;
  instructionInSourceLanguage?: string;
  toneGuidance?: string;
  speakerRole?: string;
  listenerRole?: string;
}

export type PracticeBlock =
  | 'speakingRecord'
  | 'vocabPractice'
  | 'listeningPractice'
  | 'writingPractice'
  | 'exerciseRenderer';

export type FeedbackLayout = 'pattern' | 'legacy';

export interface BaseTeachViewModel {
  block: Exclude<TeachBlock, 'patternLearning'>;
  skillBadge: SkillBadge;
  ctaLabel: string;
  ctaAction: 'startPractice' | 'startWriting';
}

export type TeachViewModel = BaseTeachViewModel | PatternLearningViewModel;

export interface PracticeViewModel {
  block: PracticeBlock;
  skillBadge: SkillBadge;
}

/**
 * One presenter per ExercisePatternKey (PatternBackedPresenter) or per legacy
 * ActivityType (Legacy*Presenter bridges). Templates switch on
 * `.block`/`.layout` instead of `activityType`/`interactionMode` directly, so
 * new pattern keys only need a PatternBackedPresenter branch, not template changes.
 */
export interface ActivityPagePresenter {
  teachContent(activity: ActivityDto): TeachViewModel;
  practiceContent(activity: ActivityDto): PracticeViewModel;
  feedbackLayout(feedback: ActivityFeedbackDto): FeedbackLayout;
}

export function defaultFeedbackLayout(feedback: ActivityFeedbackDto): FeedbackLayout {
  return feedback.patternEvaluation ? 'pattern' : 'legacy';
}
