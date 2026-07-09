// Phase H5 — Module Definition foundation. Reusable, reviewable learning units combining one or
// more Learn Items and Activity Definitions plus a module-level feedback plan — the top of the
// content-studio hierarchy (Resource Bank Item → Learn Item/Activity Definition → Module
// Definition). Distinct from the existing runtime LearningModule (a per-student thematic group
// within a LearningPath). Every create/generate action stages a pending-review row. Not assigned
// to students, not wired into Today/Practice Gym runtime this phase.

export interface ModuleLearnItemLinkDto {
  linkId: string;
  learnItemId: string;
  role: string;
  sortOrder: number;
  snapshotTitle: string | null;
}

export interface ModuleActivityLinkDto {
  linkId: string;
  activityDefinitionId: string;
  role: string;
  sortOrder: number;
  required: boolean;
  snapshotTitle: string | null;
}

export interface ModuleDefinitionDto {
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
  learnItemLinks: ModuleLearnItemLinkDto[];
  activityLinks: ModuleActivityLinkDto[];
}

export interface ModuleDefinitionListResult {
  items: ModuleDefinitionDto[];
  totalCount: number;
}

export interface ModuleLearnItemLinkInput {
  learnItemId: string;
  role: string;
}

export interface ModuleActivityLinkInput {
  activityDefinitionId: string;
  role: string;
  required?: boolean;
}

export interface GenerateModuleFromItemsRequestBody {
  learnItemLinks: ModuleLearnItemLinkInput[];
  activityLinks: ModuleActivityLinkInput[];
  title?: string | null;
  notes?: string | null;
}

export interface GenerateModuleFromResourceRequestBody {
  resourceType: string;
  resourceId: string;
  title?: string | null;
  notes?: string | null;
}

export interface GenerateModuleFromLearnItemRequestBody {
  learnItemId: string;
  title?: string | null;
  notes?: string | null;
}

export interface GenerateModuleFromActivityRequestBody {
  activityDefinitionId: string;
  title?: string | null;
  notes?: string | null;
}

export interface GenerateModuleDefinitionResult {
  module: ModuleDefinitionDto;
  reviewRoute: string;
}

export const MODULE_REVIEW_STATUSES = ['NotRequired', 'PendingReview', 'Approved', 'Rejected'] as const;
export const MODULE_SOURCE_MODES = ['Manual', 'GeneratedFromLearnAndActivities', 'GeneratedFromResources', 'Imported'] as const;
export const MODULE_LEARN_ITEM_ROLES = ['Primary', 'Supporting'] as const;
export const MODULE_ACTIVITY_ROLES = ['PrimaryPractice', 'SupportingPractice', 'Review', 'Extension'] as const;
