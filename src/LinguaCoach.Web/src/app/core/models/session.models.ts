export type SessionStatus = 'notStarted' | 'inProgress' | 'completed';
export type ExerciseStatus = 'notStarted' | 'inProgress' | 'completed' | 'skipped';

/** Phase H6 (renamed I4 Pass 3) — student-safe Lesson projection within a Today Plan module section. */
export interface TodayPlanLessonView {
  lessonId: string;
  title: string;
  body: string;
  examples: string[];
  commonMistakes: string[];
  usageNotes: string | null;
}

/** Phase H6 (renamed I4 Pass 3) — student-safe Exercise projection. Never carries an answer key or
 * scoring rules — those are backend-only per Exercise's own contract. */
export interface TodayPlanActivityView {
  exerciseId: string;
  title: string;
  description: string | null;
  instructions: string;
  activityType: string;
  formSchemaJson: string | null;
  estimatedMinutes: number | null;
}

export interface SelectedTodayPlanModule {
  moduleId: string;
  title: string;
  description: string | null;
  cefrLevel: string | null;
  skill: string | null;
  subskill: string | null;
  difficultyBand: number | null;
  estimatedMinutes: number | null;
  reason: string;
  linkedLessons: TodayPlanLessonView[];
  linkedExercises: TodayPlanActivityView[];
}

/** Phase H6 (renamed I4 Pass 3) — additive, optional. Null when no compatible approved Module
 * exists; the `exercises` above remain the source of truth in that case. */
export interface TodayPlanModuleSection {
  selectedModules: SelectedTodayPlanModule[];
  fallbackRequired: boolean;
  fallbackReason: string | null;
  selectionReason: string | null;
  targetCefrLevel: string | null;
  totalEstimatedMinutes: number;
  warnings: string[];
}

/** Returned by GET /api/sessions/today. Phase I2B — Today is module-only: `available` is the
 * single honest signal of whether there is anything to show; when false, `todayPlan` is null
 * (or reports its own fallback state) and the UI must render a "nothing available yet" state.
 * Field renamed from `moduleSection` to `todayPlan` in Phase I4 Pass 3. */
export interface TodaysSessionResponse {
  available: boolean;
  todayPlan: TodayPlanModuleSection | null;
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
