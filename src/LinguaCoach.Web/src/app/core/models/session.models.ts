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
}

/** Returned by GET /api/sessions/{id} */
export interface SessionDetailResponse {
  sessionId: string;
  title: string;
  topic: string;
  sessionGoal: string;
  durationMinutes: number;
  focusSkill: string;
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
