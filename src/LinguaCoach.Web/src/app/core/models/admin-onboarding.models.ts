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

export interface SaveDraftRequest {
  formIoSchemaJson: string;
  scoringRulesJson?: string;
  rendererKind?: FormRendererKind;
}
