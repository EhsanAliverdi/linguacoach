import { FormRendererKind } from '../../shared/formio/form-renderer-kind.model';

export interface AdminPlacementItemDto {
  itemId: string;
  skill: string;
  cefrLevel: string;
  itemOrder: number;
  isEnabled: boolean;
  /** Native Form.io schema — what the student sees. */
  formIoSchemaJson: string | null;
  /** Backend-only correct-answer data for the Form.io schema (admin-visible, never sent to students). */
  scoringRulesJson: string | null;
  scoringRulesVersion: number;
  rendererKind: FormRendererKind;
  /** Read-only preview of the schema's first component label, for the admin list only —
   * never persisted, always derived fresh from formIoSchemaJson. */
  questionPreview: string;
  /** Admin-only: the Form.io schema as authored (with inline "quiz" annotations), null for items
   * authored before the Quiz tab existed. Never sent to students. */
  authoringSchemaJson: string | null;
}

/** Server-side paged response. Items is the current page only; totalCount reflects the current
 * skill filter (drives pagination); overallTotalCount/enabledCount/skillCount are always
 * unfiltered, global bank stats for the KPI strip. */
export interface AdminPlacementItemListResult {
  items: AdminPlacementItemDto[];
  totalCount: number;
  overallTotalCount: number;
  enabledCount: number;
  skillCount: number;
}

export interface PlacementItemRequest {
  skill: string;
  cefrLevel: string;
  itemOrder: number;
  isEnabled: boolean;
  /** Placeholder — ignored by the server whenever authoringSchemaJson is present (Quiz-tab path). */
  formIoSchemaJson: string;
  /** Placeholder — ignored by the server whenever authoringSchemaJson is present (Quiz-tab path). */
  scoringRulesJson: string;
  rendererKind?: FormRendererKind;
  /** The Form.io builder's live schema, with inline per-component "quiz" annotations. The server
   * splits this into the student-safe schema + backend-only scoring rules. */
  authoringSchemaJson?: string;
}

export const PLACEMENT_SKILLS = ['grammar', 'vocabulary', 'reading', 'listening', 'writing', 'speaking'] as const;

export const PLACEMENT_CEFR_LEVELS = ['A1', 'A2', 'B1', 'B2'] as const;
