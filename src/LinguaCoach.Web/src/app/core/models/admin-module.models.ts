// Phase H5 — Module foundation. Reusable, reviewable learning units combining one or
// more Lessons and Exercises plus a module-level feedback plan — the top of the
// content-studio hierarchy (Resource Bank Item → Lesson/Exercise → Module). Distinct from the
// existing runtime LearningModule (a per-student thematic group within a LearningPath). Every
// create/generate action stages a pending-review row. Not assigned to students, not wired into
// Today/Practice Gym runtime this phase.

import { DiagnosticIssue } from './admin-repair.models';

export interface ModuleLessonLinkDto {
  linkId: string;
  lessonId: string;
  role: string;
  sortOrder: number;
  snapshotTitle: string | null;
}

export interface ModuleExerciseLinkDto {
  linkId: string;
  exerciseId: string;
  role: string;
  sortOrder: number;
  required: boolean;
  snapshotTitle: string | null;
}

export interface ModuleDto {
  id: string;
  title: string;
  description: string | null;
  cefrLevel: string | null;
  skill: string | null;
  subskill: string | null;
  contextTagsJson: string;
  focusTagsJson: string;
  difficultyBand: number | null;
  estimatedMinutes: number | null;
  feedbackPlanJson: string | null;
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
  lessonLinks: ModuleLessonLinkDto[];
  exerciseLinks: ModuleExerciseLinkDto[];
  isArchived: boolean;
}

export interface ModuleListResult {
  items: ModuleDto[];
  totalCount: number;
}

// ── Phase K5 — admin edit ────────────────────────────────────────────────────

export interface UpdateModuleRequestBody {
  title: string;
  description?: string | null;
  cefrLevel?: string | null;
  skill?: string | null;
  subskill?: string | null;
  contextTags?: string[] | null;
  focusTags?: string[] | null;
  difficultyBand?: number | null;
  estimatedMinutes?: number | null;
  feedbackPlanJson?: string | null;
}

export interface ModuleLessonLinkInput {
  lessonId: string;
  role: string;
}

export interface ModuleExerciseLinkInput {
  exerciseId: string;
  role: string;
  required?: boolean;
}

export interface GenerateModuleFromItemsRequestBody {
  lessonLinks: ModuleLessonLinkInput[];
  exerciseLinks: ModuleExerciseLinkInput[];
  title?: string | null;
  notes?: string | null;
}

/** Phase K3 — manual "author a Module by hand" create. Unlike the generate-* actions, the
 *  backend create handler does NOT require the linked Lesson/Exercise to already be approved
 *  (see AdminCreateModuleHandler's requireApproved: false). */
export interface CreateModuleRequestBody {
  title: string;
  lessonLinks: ModuleLessonLinkInput[];
  exerciseLinks: ModuleExerciseLinkInput[];
  description?: string | null;
  cefrLevel?: string | null;
  skill?: string | null;
  subskill?: string | null;
  contextTags?: string[] | null;
  focusTags?: string[] | null;
  difficultyBand?: number | null;
  estimatedMinutes?: number | null;
  feedbackPlanJson?: string | null;
}

export interface GenerateModuleFromResourceRequestBody {
  resourceType: string;
  resourceId: string;
  title?: string | null;
  notes?: string | null;
}

export interface GenerateModuleFromLessonRequestBody {
  lessonId: string;
  title?: string | null;
  notes?: string | null;
}

export interface GenerateModuleFromExerciseRequestBody {
  exerciseId: string;
  title?: string | null;
  notes?: string | null;
}

export interface GenerateModuleResult {
  module: ModuleDto;
  reviewRoute: string;
}

// Phase J3 — admin "preview as a learner". Lets an admin render a Module's Lesson + Exercise
// exactly as a student would, submit an answer, and see a real score/feedback — before the
// Module is approved. Never exposes an answer key or scoring rules.

export interface ModulePreviewLessonDto {
  lessonId: string;
  title: string;
  body: string;
  examples: string[];
  commonMistakes: string[];
  usageNotes: string | null;
}

export interface ModulePreviewExerciseDto {
  exerciseId: string;
  title: string;
  instructions: string;
  activityType: string;
  rendererType: string;
  formSchemaJson: string | null;
  estimatedMinutes: number | null;
  canScore: boolean;
  unscorableReason: string | null;
}

export interface ModulePreviewResult {
  moduleId: string;
  moduleTitle: string;
  moduleDescription: string | null;
  moduleReviewStatus: string;
  lesson: ModulePreviewLessonDto | null;
  exercise: ModulePreviewExerciseDto | null;
  moduleFeedbackPlanJson: string | null;
}

export interface ModulePreviewSubmitRequestBody {
  answers: Record<string, unknown>;
}

export interface ModulePreviewComponentResult {
  componentKey: string;
  isCorrect: boolean;
  pointsEarned: number;
  maxPoints: number;
}

export interface ModulePreviewSubmitResult {
  scored: boolean;
  unscorableReason: string | null;
  scorePercent: number | null;
  allCorrect: boolean | null;
  components: ModulePreviewComponentResult[];
  feedbackMessage: string | null;
}

export const MODULE_REVIEW_STATUSES = ['NotRequired', 'PendingReview', 'Approved', 'Rejected'] as const;
export const MODULE_SOURCE_MODES = ['Manual', 'GeneratedFromLessonAndExercises', 'GeneratedFromResources', 'Imported'] as const;
export const MODULE_LESSON_ROLES = ['Primary', 'Supporting'] as const;
export const MODULE_EXERCISE_ROLES = ['PrimaryPractice', 'SupportingPractice', 'Review', 'Extension'] as const;

// ── Phase K6 — admin archive/unarchive (soft-delete) ────────────────────────────────
export interface ModuleArchiveItemResult {
  id: string;
  success: boolean;
  error: string | null;
}
export interface ModuleArchiveResult {
  requestedCount: number;
  succeededCount: number;
  failedCount: number;
  items: ModuleArchiveItemResult[];
}

// ── Phase K8 — "Fix with AI" repair ──────────────────────────────────────────
export interface ModuleRepairResult {
  item: ModuleDto;
  issuesFixed: DiagnosticIssue[];
  issuesRemaining: DiagnosticIssue[];
  providerName: string | null;
  modelName: string | null;
}
