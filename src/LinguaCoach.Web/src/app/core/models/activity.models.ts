export type ActivityType =
  | 'writingScenario'
  | 'speakingRolePlay'
  | 'listeningComprehension'
  | 'vocabularyPractice'
  | 'pronunciationPractice'
  | 'readingTask';

export type ActivitySource = 'aiGenerated' | 'systemFallback';

export interface VocabPracticeItem {
  vocabularyItemId: string;
  term: string;
  prompt: string;
  hint: string;
  explanation: string;
}

export interface ActivityDto {
  activityId: string;
  activityType: ActivityType;
  source: ActivitySource;
  title: string;
  difficulty: string;
  // WritingScenario fields — null for other activity types
  situation: string | null;
  learningGoal: string | null;
  targetPhrases: string[];
  targetVocabulary: string[];
  exampleText: string | null;
  commonMistakeToAvoid: string | null;
  instructionInSourceLanguage: string | null;
  // VocabularyPractice fields — null for other activity types
  instructions: string | null;
  practiceMode: string | null;
  vocabItems: VocabPracticeItem[] | null;
}

export interface VocabAnswer {
  vocabularyItemId: string;
  answer: string;
}

export type FeedbackChangeType = 'replace' | 'add' | 'remove' | 'reorder';
export type FeedbackChangeCategory = 'grammar' | 'vocabulary' | 'tone' | 'clarity' | 'structure' | 'punctuation';
export type FeedbackChangeSeverity = 'high' | 'medium' | 'low';

export interface FeedbackChangeDto {
  type: FeedbackChangeType;
  original: string | null;
  suggested: string | null;
  reason: string | null;
  category: FeedbackChangeCategory | null;
  severity: FeedbackChangeSeverity | null;
}

export interface ActivityFeedbackDto {
  attemptId: string;
  score: number | null;
  // Coach summary
  coachSummary: string | null;
  // Focus mode: true when many issues exist and list is limited to top 3-5
  focusFirst: boolean;
  // Targeted change list — primary coaching output
  changes: FeedbackChangeDto[];
  // Improved version (alias: correctedText kept for backward compat)
  correctedText: string | null;
  whatYouDidWell: string[];
  mainMistakes: string[];
  grammarIssues: string[];
  vocabularyIssues: string[];
  toneIssues: string[];
  clarityIssues: string[];
  grammarExplanation: string | null;
  toneExplanation: string | null;
  vocabularyToRemember: string[];
  miniLesson: string | null;
  nextImprovementStep: string | null;
  rewriteChallenge: string | null;
  nextPracticeSuggestion: string | null;
  feedbackInSourceLanguage: string | null;
}
