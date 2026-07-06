import { FormRendererKind } from '../../shared/formio/form-renderer-kind.model';

export interface AdminPlacementItemDto {
  itemId: string;
  skill: string;
  cefrLevel: string;
  itemType: string;
  prompt: string;
  itemOrder: number;
  isEnabled: boolean;
  /** Native Form.io schema — what the student sees. */
  formIoSchemaJson: string | null;
  /** Backend-only correct-answer data for the Form.io schema (admin-visible, never sent to students). */
  scoringRulesJson: string | null;
  scoringRulesVersion: number;
  rendererKind: FormRendererKind;
}

export interface PlacementItemRequest {
  skill: string;
  cefrLevel: string;
  itemType: string;
  prompt: string;
  itemOrder: number;
  isEnabled: boolean;
  formIoSchemaJson: string;
  scoringRulesJson: string;
  rendererKind?: FormRendererKind;
}

export const PLACEMENT_SKILLS = ['grammar', 'vocabulary', 'reading', 'listening', 'writing', 'speaking'] as const;

export const PLACEMENT_CEFR_LEVELS = ['A1', 'A2', 'B1', 'B2'] as const;

/** Item-type label used for admin list display and grouping — not a rendering constraint;
 * the Form.io schema itself defines the actual component(s) shown to the student. */
export const PLACEMENT_ITEM_TYPES = ['multiple_choice', 'gap_fill'] as const;
