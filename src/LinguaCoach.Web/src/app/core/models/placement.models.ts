import { QuestionContent } from '../../shared/question/question-content.models';

export type PlacementStatusValue = 'NotStarted' | 'InProgress' | 'Completed';

export interface PlacementQuestion {
  key: string;
  prompt: string;
  type: 'rating' | 'choice' | 'text';
  options?: string[];
}

export interface PlacementSection {
  key: string;
  order: number;
  title: string;
  instructions: string;
  sectionType: 'self_check' | 'mcq' | 'reading' | 'listening' | 'writing' | 'speaking';
  scored: boolean;
  questions: PlacementQuestion[];
  passage?: string | null;
  audioScript?: string | null;
  writingPrompt?: string | null;
  speakingPrompt?: string | null;
}

export interface PlacementStatus {
  status: PlacementStatusValue;
  currentSectionKey: string;
  currentSectionOrder: number;
  totalSections: number;
  lifecycleStage: string;
  isCompleted: boolean;
}

export interface PlacementCurrentSection {
  status: PlacementStatusValue;
  section: PlacementSection | null;
  currentSectionOrder: number;
  totalSections: number;
  isCompleted: boolean;
  /** Relative API URL for server-side listening audio. Only set for the listening section. */
  audioUrl?: string | null;
  /** True when server-side TTS audio is available and ready to play. */
  audioAvailable?: boolean;
}

export interface PlacementAnswerInput {
  questionKey: string;
  responseText?: string | null;
  selectedOption?: string | null;
}

export interface SavePlacementAnswers {
  sectionKey: string;
  answers: PlacementAnswerInput[];
}

export interface PlacementSkillLevel {
  skill: string;
  level: string;
}

export interface PlacementResult {
  estimatedOverallLevel: string;
  skillLevels: PlacementSkillLevel[];
  strengths: string[];
  weaknesses: string[];
  recommendedStartingCourse?: string | null;
  recommendedSessionDuration?: number | null;
  placementNotes?: string | null;
  isCompleted: boolean;
}

// ── Phase 14A — Adaptive placement models ───────────────────────────────────

export interface AdaptivePlacementSkillResult {
  skill: string;
  estimatedCefrLevel: string;
  confidence: number;
  evidenceCount: number;
  strengths: string | null;
  weaknesses: string | null;
  recommendedObjectiveKeys: string[];
}

export interface AdaptivePlacementSummary {
  assessmentId: string;
  studentProfileId: string;
  status: 'NotStarted' | 'InProgress' | 'Completed' | 'Abandoned' | 'Expired' | 'Failed';
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  expiredAtUtc: string | null;
  overallCefrLevel: string | null;
  overallConfidence: number | null;
  isProvisional: boolean;
  resultSummary: string | null;
  source: string | null;
  skillResults: AdaptivePlacementSkillResult[];
  learningPlanRegenerated: boolean;
  learningPlanRegenerationWarning: string | null;
  itemCount: number;
  hasPlacement?: boolean;
}

export interface AdaptivePlacementNextItem {
  itemId: string;
  skill: string;
  targetCefrLevel: string;
  itemType: 'multiple_choice' | 'gap_fill';
  prompt: string;
  itemOrder: number;
  answeredCount: number;
  estimatedRemainingItems: number;
  readingPassage?: string | null;
  hasAudio?: boolean;
  /** Unified Question-Schema (Phase 2/3) — the shared, polymorphic representation of this
   * item. Present for every item once the backend backfill has run; the legacy flat fields
   * above are kept only as a defensive fallback. */
  content?: QuestionContent | null;
}

export interface AdaptivePlacementRespondRequest {
  assessmentId: string;
  itemId: string;
  response: string;
  durationSeconds?: number | null;
}

export interface AdaptivePlacementSubmitResult {
  itemId: string;
  isCorrect: boolean;
  score: number;
  evaluationNotes: string;
  assessmentComplete: boolean;
  completionReason: string | null;
  nextItem: AdaptivePlacementNextItem | null;
  summary: AdaptivePlacementSummary | null;
}

export interface PlacementConfig {
  placementRequiredBeforeLearning: boolean;
  allowSkipPlacement: boolean;
  allowPlacementRetake: boolean;
  autoStartPlacement: boolean;
}
