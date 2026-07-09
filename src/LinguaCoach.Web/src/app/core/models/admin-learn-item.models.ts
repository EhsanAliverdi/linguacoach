// Phase H3 — Learn Item foundation. Reviewable teaching/explanation blocks generated from (or
// manually authored about) selected published Resource Bank rows — the "Learn" half of a future
// Module. Every create/generate action stages a pending-review row; nothing here creates an
// Activity/Module row or assigns anything to a student.

export interface LearnItemResourceLinkDto {
  linkId: string;
  resourceType: string;
  resourceId: string;
  role: string;
  snapshotTitle: string | null;
  contentFingerprint: string | null;
}

export interface LearnItemDto {
  id: string;
  title: string;
  body: string;
  examplesJson: string;
  commonMistakesJson: string;
  usageNotes: string | null;
  cefrLevel: string | null;
  skill: string | null;
  subskill: string | null;
  contextTagsJson: string;
  focusTagsJson: string;
  difficultyBand: number | null;
  estimatedMinutes: number | null;
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
  links: LearnItemResourceLinkDto[];
}

export interface LearnItemListResult {
  items: LearnItemDto[];
  totalCount: number;
}

export interface LearnItemResourceLinkInput {
  resourceType: string;
  resourceId: string;
  role: string;
}

export interface CreateLearnItemRequestBody {
  title: string;
  body: string;
  cefrLevel?: string | null;
  skill?: string | null;
  subskill?: string | null;
  contextTags?: string[] | null;
  focusTags?: string[] | null;
  examples?: string[] | null;
  commonMistakes?: string[] | null;
  usageNotes?: string | null;
  difficultyBand?: number | null;
  estimatedMinutes?: number | null;
  links?: LearnItemResourceLinkInput[] | null;
}

export interface UpdateLearnItemRequestBody {
  title: string;
  body: string;
  examples?: string[] | null;
  commonMistakes?: string[] | null;
  usageNotes?: string | null;
  cefrLevel?: string | null;
  skill?: string | null;
  subskill?: string | null;
  contextTags?: string[] | null;
  focusTags?: string[] | null;
  difficultyBand?: number | null;
  estimatedMinutes?: number | null;
}

export interface GenerateLearnItemFromResourcesRequestBody {
  resources: LearnItemResourceLinkInput[];
  title?: string | null;
  defaultCefrLevel?: string | null;
  defaultSkill?: string | null;
  defaultSubskill?: string | null;
  defaultContextTags?: string[] | null;
  defaultFocusTags?: string[] | null;
  defaultDifficultyBand?: number | null;
  notes?: string | null;
}

export interface GenerateLearnItemFromResourcesResult {
  learnItem: LearnItemDto;
  reviewRoute: string;
}

export const LEARN_ITEM_REVIEW_STATUSES = ['NotRequired', 'PendingReview', 'Approved', 'Rejected'] as const;
export const LEARN_ITEM_SOURCE_MODES = ['Manual', 'GeneratedFromResources', 'Imported'] as const;
export const LEARN_ITEM_RESOURCE_ROLES = ['Primary', 'Supporting'] as const;
