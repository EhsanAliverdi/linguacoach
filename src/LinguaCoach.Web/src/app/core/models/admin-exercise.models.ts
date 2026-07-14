// Phase H4 — Exercise foundation. Reviewable, editable practice task designs generated from (or
// authored about) selected published Resource Bank rows, optionally linked to a Lesson — the
// "Practice" half of a future Module. Distinct from the existing runtime LearningActivity
// (per-student delivery record) and the legacy ActivityTemplate Form.io pilot (removed in I2A).
// Every create/generate action stages a pending-review row.

import { DiagnosticIssue } from './admin-repair.models';

export interface ExerciseResourceLinkDto {
  linkId: string;
  resourceType: string;
  resourceId: string;
  role: string;
  snapshotTitle: string | null;
  contentFingerprint: string | null;
}

export interface ExerciseDto {
  id: string;
  title: string;
  description: string | null;
  instructions: string;
  activityType: string;
  patternKey: string | null;
  rendererType: string;
  formSchemaJson: string | null;
  answerKeyJson: string | null;
  scoringRulesJson: string | null;
  feedbackPlanJson: string | null;
  cefrLevel: string | null;
  skill: string | null;
  subskill: string | null;
  contextTagsJson: string;
  focusTagsJson: string;
  difficultyBand: number | null;
  estimatedMinutes: number | null;
  lessonId: string | null;
  sourceMode: string;
  generationProvider: string | null;
  generationModel: string | null;
  reviewStatus: string;
  createdByUserId: string | null;
  reviewedByUserId: string | null;
  approvedAtUtc: string | null;
  rejectedAtUtc: string | null;
  rejectionReason: string | null;
  reviewNotes: string | null;
  createdAt: string;
  updatedAtUtc: string;
  links: ExerciseResourceLinkDto[];
  /** Phase J4 — whether this activity type/content would be launchable to a student once
   *  approved, independent of this Exercise's own current review status. A draft can be
   *  canLaunchOnceApproved=false (e.g. "short_answer" has no auto-scoring path yet) while still
   *  being a legitimate, reviewable, approvable draft — this only tells the admin honestly
   *  whether it will ever reach a student. */
  canLaunchOnceApproved: boolean;
  launchUnsupportedReason: string | null;
  isArchived: boolean;
}

export interface ExerciseListResult {
  items: ExerciseDto[];
  totalCount: number;
}

export interface ExerciseResourceLinkInput {
  resourceType: string;
  resourceId: string;
  role: string;
}

export interface GenerateActivityFromResourcesRequestBody {
  resources: ExerciseResourceLinkInput[];
  requestedActivityType?: string | null;
  title?: string | null;
  defaultCefrLevel?: string | null;
  defaultSkill?: string | null;
  defaultSubskill?: string | null;
  defaultContextTags?: string[] | null;
  defaultFocusTags?: string[] | null;
  defaultDifficultyBand?: number | null;
  notes?: string | null;
}

export interface GenerateActivityFromLessonRequestBody {
  lessonId: string;
  requestedActivityType?: string | null;
  title?: string | null;
  notes?: string | null;
}

export interface GenerateExerciseResult {
  activity: ExerciseDto;
  reviewRoute: string;
}

// ── Phase K5 — admin edit ────────────────────────────────────────────────────

export interface UpdateExerciseRequestBody {
  title: string;
  instructions: string;
  description?: string | null;
  formSchemaJson?: string | null;
  answerKeyJson?: string | null;
  scoringRulesJson?: string | null;
  feedbackPlanJson?: string | null;
  cefrLevel?: string | null;
  skill?: string | null;
  subskill?: string | null;
  contextTags?: string[] | null;
  focusTags?: string[] | null;
  difficultyBand?: number | null;
  estimatedMinutes?: number | null;
}

// ── Phase K5 — "Generate Exercises from Lesson" with an admin-picked count/type per Exercise.
// Module creation is automatic afterward — see AdminModuleService docs. ───────────────────────

export interface ActivityGenerationSpec {
  /** Null/omitted means "auto-pick" — same per-resource-type default the single-item endpoint uses. */
  activityType?: string | null;
  count: number;
}

export interface GenerateActivitiesFromLessonRequestBody {
  lessonId: string;
  specs: ActivityGenerationSpec[];
  titlePrefix?: string | null;
  notes?: string | null;
}

export interface GenerateActivitiesFromLessonResult {
  activities: ExerciseDto[];
  moduleId: string;
  moduleReviewRoute: string;
}

export const ACTIVITY_REVIEW_STATUSES = ['NotRequired', 'PendingReview', 'Approved', 'Rejected'] as const;
export const ACTIVITY_SOURCE_MODES = ['Manual', 'GeneratedFromResources', 'GeneratedFromLesson', 'Imported'] as const;
export const ACTIVITY_RENDERER_TYPES = ['Formio', 'Custom', 'Legacy'] as const;
export const ACTIVITY_TYPES = ['gap_fill', 'multiple_choice_single', 'short_answer'] as const;

// ── Phase K7 — admin "preview as a learner" for a standalone Exercise ────────────────
export interface ExercisePreviewComponentResult {
  componentKey: string;
  isCorrect: boolean;
  pointsEarned: number;
  maxPoints: number;
}
export interface ExercisePreviewSubmitRequestBody {
  answers: Record<string, unknown>;
}
export interface ExercisePreviewSubmitResult {
  scored: boolean;
  unscorableReason: string | null;
  scorePercent: number | null;
  allCorrect: boolean | null;
  components: ExercisePreviewComponentResult[];
  feedbackMessage: string | null;
}

// ── Phase K8 — "Fix with AI" repair ──────────────────────────────────────────
export interface ExerciseRepairResult {
  item: ExerciseDto;
  issuesFixed: DiagnosticIssue[];
  issuesRemaining: DiagnosticIssue[];
  providerName: string | null;
  modelName: string | null;
}

// ── Phase K6 — admin archive/unarchive (soft-delete) ────────────────────────────────
export interface ExerciseArchiveItemResult {
  id: string;
  success: boolean;
  error: string | null;
}
export interface ExerciseArchiveResult {
  requestedCount: number;
  succeededCount: number;
  failedCount: number;
  items: ExerciseArchiveItemResult[];
}
