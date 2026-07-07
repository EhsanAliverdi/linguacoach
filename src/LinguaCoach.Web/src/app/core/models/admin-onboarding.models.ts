// Form.io migration (onboarding half) — replaces the old per-step-type AdminOnboardingFlow
// models. Onboarding is now a single admin-authored Form.io wizard: one StudentFlowTemplate,
// versioned (Draft -> Published -> Archived), with correct-answer data for the two quick-check
// radios kept out-of-schema in ScoringRulesJson.

import { FormRendererKind } from '../../shared/formio/form-renderer-kind.model';

export type StudentFlowTemplateStatus = 'Draft' | 'Published' | 'Archived';

export interface StudentFlowTemplateSummaryDto {
  templateId: string;
  name: string;
  description: string | null;
  status: StudentFlowTemplateStatus;
  activeVersionId: string | null;
  versionCount: number;
  updatedAt: string;
}

export interface StudentFlowTemplateVersionDto {
  versionId: string;
  templateId: string;
  versionNumber: number;
  formIoSchemaJson: string;
  scoringRulesJson: string | null;
  rendererKind: FormRendererKind;
  status: StudentFlowTemplateStatus;
  publishedAt: string | null;
  updatedAt: string;
  /** Admin-only: the Form.io schema as authored (with inline "quiz" annotations), null for
   * versions authored before the Quiz tab existed. Never sent to students. */
  authoringSchemaJson: string | null;
}

export interface StudentFlowTemplateDetailDto {
  templateId: string;
  name: string;
  description: string | null;
  status: StudentFlowTemplateStatus;
  activeVersionId: string | null;
  versions: StudentFlowTemplateVersionDto[];
}

export interface CreateTemplateRequest {
  name: string;
  description?: string;
}

/** Mirrors LinguaCoach.Application.Onboarding.OnboardingProfileFieldMapping.FieldMapping — the
 * single source of truth for which component `key` StudentOnboardingFlowService.ApplyToProfileAsync
 * reads out of a submission, and what StudentProfile field/shape it expects. Served by
 * GET /admin/onboarding/profile-field-mapping so the editor's "Field mapping" panel never drifts
 * out of sync with the backend's hardcoded key list. */
export interface OnboardingFieldMappingDto {
  key: string;
  profileField: string;
  description: string;
  required: boolean;
  expectedShape: string;
}

export interface SaveDraftRequest {
  /** Placeholder — ignored by the server whenever authoringSchemaJson is present (Quiz-tab path). */
  formIoSchemaJson: string;
  /** Placeholder — ignored by the server whenever authoringSchemaJson is present (Quiz-tab path). */
  scoringRulesJson?: string;
  rendererKind?: FormRendererKind;
  /** The Form.io builder's live schema, with inline per-component "quiz" annotations. The server
   * splits this into the student-safe schema + backend-only scoring rules. */
  authoringSchemaJson?: string;
}
