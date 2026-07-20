import { FormRendererKind } from '../../shared/formio/form-renderer-kind.model';

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
  /** Backend value is the Form.io component type extracted from the item's own schema
   * (PlacementItemSchemaLabel.ExtractComponentType) — not the legacy pre-Form.io
   * "multiple_choice"/"gap_fill" labels. Typed loosely since new component types can be added
   * to the item bank without a frontend contract change. */
  itemType: string;
  prompt: string;
  itemOrder: number;
  answeredCount: number;
  estimatedRemainingItems: number;
  hasAudio?: boolean;
  /** Native Form.io authoring — always populated by the backend now. A JSON string; parse
   * before feeding to FormioRendererComponent. */
  formIoSchemaJson?: string | null;
  rendererKind?: FormRendererKind;
}

export interface AdaptivePlacementRespondRequest {
  assessmentId: string;
  itemId: string;
  /** Full Form.io submission payload — the raw `.data` dictionary from the rendered form. */
  submission: { data: Record<string, unknown> };
  durationSeconds?: number | null;
  /** Placement-cards flow: scopes adaptive item selection to this one skill. */
  skill?: string | null;
}

/** One card's status on the placement-cards landing page. */
export interface PlacementSkillStatus {
  skill: string;
  label: string;
  percentComplete: number;
  completed: boolean;
  evidenceCount: number;
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
