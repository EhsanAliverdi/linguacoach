export type AdminReviewStatusValue = 'NotRequired' | 'PendingReview' | 'Approved' | 'Rejected';

export interface AdminActivityTemplateDto {
  templateId: string;
  key: string;
  versionNumber: number;
  previousVersionId: string | null;
  skill: string;
  subskill: string | null;
  cefrLevel: string;
  contextTagsJson: string;
  focusTagsJson: string;
  curriculumObjectiveKey: string | null;
  activityType: string;
  patternKey: string | null;
  /** Student-safe Form.io base schema. */
  formIoBaseSchemaJson: string | null;
  /** Backend-only: prompt fragment/constraints for future AI personalization. Never sent to students. */
  generationInstructions: string | null;
  /** Backend-only: scoring model for this template. Never sent to students. */
  scoringModelJson: string | null;
  /** Backend-only: validation constraints an AI-personalized instance must satisfy. */
  validationRulesJson: string | null;
  reviewStatus: AdminReviewStatusValue;
  isPublished: boolean;
  estimatedDurationSeconds: number | null;
  assetRequirementsJson: string | null;
}

/** Server-side paged response. Items is the current page only; totalCount reflects the current
 * filters (drives pagination); overallTotalCount/publishedCount/skillCount are always
 * unfiltered, global bank stats for the KPI strip. */
export interface AdminActivityTemplateListResult {
  items: AdminActivityTemplateDto[];
  totalCount: number;
  overallTotalCount: number;
  publishedCount: number;
  skillCount: number;
}

export interface ActivityTemplateCreateRequest {
  key: string;
  skill: string;
  cefrLevel: string;
  activityType: string;
  subskill?: string | null;
  patternKey?: string | null;
  contextTagsJson?: string;
  focusTagsJson?: string;
  curriculumObjectiveKey?: string | null;
  formIoBaseSchemaJson?: string | null;
  generationInstructions?: string | null;
  scoringModelJson?: string | null;
  validationRulesJson?: string | null;
  estimatedDurationSeconds?: number | null;
  assetRequirementsJson?: string | null;
}

export type ActivityTemplateUpdateRequest = Omit<ActivityTemplateCreateRequest, 'key'>;

export interface ActivityTemplateReviewRequest {
  action: 'approve' | 'reject' | 'reset';
  reason?: string | null;
}

export interface ActivityTemplatePublishRequest {
  publish: boolean;
}

export interface ActivityTemplateGeneratePreviewRequest {
  cefrLevelOverride?: string | null;
  topicHint?: string | null;
}

export interface ActivityTemplateInstanceResult {
  templateId: string;
  generatedSchemaJson: string;
  providerName: string;
  modelName: string;
  correlationId: string | null;
}

export const ACTIVITY_TEMPLATE_SKILLS = [
  'writing', 'reading', 'listening', 'speaking', 'vocabulary', 'grammar', 'pronunciation', 'fluency', 'confidence',
] as const;

export const ACTIVITY_TEMPLATE_CEFR_LEVELS = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'] as const;

export const ACTIVITY_TEMPLATE_REVIEW_STATUSES: AdminReviewStatusValue[] = [
  'NotRequired', 'PendingReview', 'Approved', 'Rejected',
];
