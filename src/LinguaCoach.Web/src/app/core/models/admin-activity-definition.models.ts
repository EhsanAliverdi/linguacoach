// Phase H4 — Activity foundation. Reviewable, editable practice task designs generated from (or
// authored about) selected published Resource Bank rows, optionally linked to a Learn Item — the
// "Practice" half of a future Module. Distinct from the existing runtime LearningActivity
// (per-student delivery record) and ActivityTemplate (already wired into the live Practice Gym
// Form.io pilot). Every create/generate action stages a pending-review row.

export interface ActivityResourceLinkDto {
  linkId: string;
  resourceType: string;
  resourceId: string;
  role: string;
  snapshotTitle: string | null;
  contentFingerprint: string | null;
}

export interface ActivityDefinitionDto {
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
  learnItemId: string | null;
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
  links: ActivityResourceLinkDto[];
}

export interface ActivityDefinitionListResult {
  items: ActivityDefinitionDto[];
  totalCount: number;
}

export interface ActivityResourceLinkInput {
  resourceType: string;
  resourceId: string;
  role: string;
}

export interface GenerateActivityFromResourcesRequestBody {
  resources: ActivityResourceLinkInput[];
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

export interface GenerateActivityFromLearnItemRequestBody {
  learnItemId: string;
  requestedActivityType?: string | null;
  title?: string | null;
  notes?: string | null;
}

export interface GenerateActivityDefinitionResult {
  activity: ActivityDefinitionDto;
  reviewRoute: string;
}

export const ACTIVITY_REVIEW_STATUSES = ['NotRequired', 'PendingReview', 'Approved', 'Rejected'] as const;
export const ACTIVITY_SOURCE_MODES = ['Manual', 'GeneratedFromResources', 'GeneratedFromLearnItem', 'Imported'] as const;
export const ACTIVITY_RENDERER_TYPES = ['Formio', 'Custom', 'Legacy'] as const;
export const ACTIVITY_TYPES = ['gap_fill', 'multiple_choice_single', 'short_answer'] as const;
