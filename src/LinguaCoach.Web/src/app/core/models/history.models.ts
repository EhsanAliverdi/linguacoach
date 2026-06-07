export interface ActivitySummary {
  activityId: string;
  title: string;
  activityType: string;
  attemptCount: number;
  bestScore: number | null;
  latestScore: number | null;
  latestAttemptAt: string | null;
  hasFeedback: boolean;
}

export interface ModuleActivityHistory {
  moduleId: string;
  title: string;
  description: string;
  completedActivities: number;
  totalRequired: number;
  averageScore: number | null;
  latestScore: number | null;
  isReadyToComplete: boolean;
  isCompleted: boolean;
  activities: ActivitySummary[];
}

export interface AttemptChange {
  type: string;
  original: string | null;
  suggested: string | null;
  reason: string | null;
  category: string | null;
  severity: string | null;
}

export interface AttemptDetail {
  attemptId: string;
  attemptNumber: number;
  submittedAt: string;
  score: number | null;
  coachSummary: string | null;
  focusFirst: boolean;
  changes: AttemptChange[];
  whatYouDidWell: string[];
  grammarIssues: string[];
  vocabularyIssues: string[];
  toneIssues: string[];
  clarityIssues: string[];
  miniLesson: string | null;
  nextImprovementStep: string | null;
  suggestedImprovedVersion: string | null;
  nativeLanguageExplanation: string | null;
  submittedContent: string | null;
  listeningQuestionFeedback: ListeningAttemptQuestion[] | null;
  transcript: string | null;
  responseFeedback: string | null;
}

export interface ListeningAttemptQuestion {
  questionId: string;
  question: string;
  studentAnswer: string;
  expectedAnswerSummary: string;
  isCorrect: boolean;
  score: number;
  feedback: string;
}

export interface ActivityAttemptHistory {
  activityId: string;
  title: string;
  activityType: string;
  situation: string | null;
  learningGoal: string | null;
  targetPhrases: string[];
  attempts: AttemptDetail[];
}
