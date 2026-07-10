// Phase H5 — Module foundation. Reusable, reviewable learning units combining one or
// more Lessons and Exercises plus a module-level feedback plan — the top of the
// content-studio hierarchy (Resource Bank Item → Lesson/Exercise → Module). Distinct from the
// existing runtime LearningModule (a per-student thematic group within a LearningPath). Every
// create/generate action stages a pending-review row. Not assigned to students, not wired into
// Today/Practice Gym runtime this phase.

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
  objectiveKey: string | null;
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
}

export interface ModuleListResult {
  items: ModuleDto[];
  totalCount: number;
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

export const MODULE_REVIEW_STATUSES = ['NotRequired', 'PendingReview', 'Approved', 'Rejected'] as const;
export const MODULE_SOURCE_MODES = ['Manual', 'GeneratedFromLessonAndExercises', 'GeneratedFromResources', 'Imported'] as const;
export const MODULE_LESSON_ROLES = ['Primary', 'Supporting'] as const;
export const MODULE_EXERCISE_ROLES = ['PrimaryPractice', 'SupportingPractice', 'Review', 'Extension'] as const;
