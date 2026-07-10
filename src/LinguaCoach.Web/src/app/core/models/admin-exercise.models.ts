// Phase H4 — Exercise foundation. Reviewable, editable practice task designs generated from (or
// authored about) selected published Resource Bank rows, optionally linked to a Lesson — the
// "Practice" half of a future Module. Distinct from the existing runtime LearningActivity
// (per-student delivery record) and the legacy ActivityTemplate Form.io pilot (removed in I2A).
// Every create/generate action stages a pending-review row.

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

export const ACTIVITY_REVIEW_STATUSES = ['NotRequired', 'PendingReview', 'Approved', 'Rejected'] as const;
export const ACTIVITY_SOURCE_MODES = ['Manual', 'GeneratedFromResources', 'GeneratedFromLesson', 'Imported'] as const;
export const ACTIVITY_RENDERER_TYPES = ['Formio', 'Custom', 'Legacy'] as const;
export const ACTIVITY_TYPES = ['gap_fill', 'multiple_choice_single', 'short_answer'] as const;
