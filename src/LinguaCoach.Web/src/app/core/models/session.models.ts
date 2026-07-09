export type SessionStatus = 'notStarted' | 'inProgress' | 'completed';
export type ExerciseStatus = 'notStarted' | 'inProgress' | 'completed' | 'skipped';
export type ExerciseKind =
  | 'vocabularyWarmup'
  | 'contextInput'
  | 'listeningInput'
  | 'readingInput'
  | 'writingTask'
  | 'speakingTask'
  | 'review';

export interface SessionExercise {
  exerciseId: string;
  order: number;
  kind: ExerciseKind;
  exercisePatternKey: string;
  primarySkill: string;
  instructions: string;
  estimatedMinutes: number;
  status: ExerciseStatus;
  learningActivityId: string | null;
}

/** Phase H6 — student-safe Learn Item projection within a Daily Lesson module section. */
export interface DailyLessonLearnItemView {
  learnItemId: string;
  title: string;
  body: string;
  examples: string[];
  commonMistakes: string[];
  usageNotes: string | null;
}

/** Phase H6 — student-safe Activity Definition projection. Never carries an answer key or
 * scoring rules — those are backend-only per ActivityDefinition's own contract. */
export interface DailyLessonActivityView {
  activityDefinitionId: string;
  title: string;
  description: string | null;
  instructions: string;
  activityType: string;
  formSchemaJson: string | null;
  estimatedMinutes: number | null;
}

export interface SelectedDailyLessonModule {
  moduleDefinitionId: string;
  title: string;
  description: string | null;
  cefrLevel: string | null;
  skill: string | null;
  subskill: string | null;
  difficultyBand: number | null;
  estimatedMinutes: number | null;
  reason: string;
  linkedLearnItems: DailyLessonLearnItemView[];
  linkedActivityDefinitions: DailyLessonActivityView[];
}

/** Phase H6 — additive, optional. Null when no compatible approved Module exists; the
 * `exercises` above remain the source of truth in that case. */
export interface DailyLessonModuleSection {
  selectedModules: SelectedDailyLessonModule[];
  fallbackRequired: boolean;
  fallbackReason: string | null;
  selectionReason: string | null;
  targetCefrLevel: string | null;
  totalEstimatedMinutes: number;
  warnings: string[];
}

/** Returned by GET /api/sessions/today */
export interface TodaysSessionResponse {
  sessionId: string;
  title: string;
  topic: string;
  sessionGoal: string;
  durationMinutes: number;
  focusSkill: string;
  status: SessionStatus;
  isResuming: boolean;
  exercises: SessionExercise[];
  moduleSection: DailyLessonModuleSection | null;
}

/** Returned by GET /api/sessions/{id} */
export interface SessionDetailResponse {
  sessionId: string;
  title: string;
  topic: string;
  sessionGoal: string;
  durationMinutes: number;
  focusSkill: string;
  cefrLevel: string | null;
  status: SessionStatus;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  exercises: SessionExercise[];
}

export interface StartSessionResponse {
  sessionId: string;
  status: SessionStatus;
  startedAtUtc: string;
}

export interface CompleteSessionResponse {
  sessionId: string;
  status: SessionStatus;
  completedAtUtc: string;
}

export interface CompleteExerciseResponse {
  exerciseId: string;
  status: ExerciseStatus;
  completedAtUtc: string;
  sessionComplete: boolean;
}

export interface PrepareExerciseResponse {
  activityId: string;
  activityType: string | null;
  isReview: boolean;
}

export interface SessionHistoryExercise {
  exerciseId: string;
  order: number;
  exercisePatternKey: string;
  primarySkill: string;
  status: ExerciseStatus;
  score: number | null;
  completedAtUtc: string | null;
}

export interface SessionHistoryItem {
  sessionId: string;
  title: string;
  topic: string;
  focusSkill: string;
  status: SessionStatus;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  exercises: SessionHistoryExercise[];
}

export interface SessionHistoryResponse {
  sessions: SessionHistoryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}
