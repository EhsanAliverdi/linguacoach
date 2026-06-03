export type ActivityType =
  | 'writingScenario'
  | 'speakingRolePlay'
  | 'listeningComprehension'
  | 'vocabularyPractice'
  | 'pronunciationPractice'
  | 'readingTask';

export type ActivitySource = 'aiGenerated' | 'systemFallback';

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
}

export interface ActivityFeedbackDto {
  attemptId: string;
  score: number | null;
  correctedText: string | null;
  whatYouDidWell: string[];
  mainMistakes: string[];
  grammarExplanation: string | null;
  toneExplanation: string | null;
  vocabularyToRemember: string[];
  rewriteChallenge: string | null;
  nextPracticeSuggestion: string | null;
  feedbackInSourceLanguage: string | null;
}
