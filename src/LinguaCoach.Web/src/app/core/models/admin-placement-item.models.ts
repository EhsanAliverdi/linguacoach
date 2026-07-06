import { QuestionContent } from '../../shared/question/question-content.models';
import { FormRendererKind } from '../../shared/formio/form-renderer-kind.model';

export interface AdminPlacementItemDto {
  itemId: string;
  skill: string;
  cefrLevel: string;
  itemType: string;
  prompt: string;
  correctAnswer: string;
  readingPassage: string | null;
  listeningAudioScript: string | null;
  itemOrder: number;
  isEnabled: boolean;
  /** Unified Question-Schema (Phase 4) — the authoritative, admin-authored content. The flat
   * fields above are derived from it for display/legacy continuity only. */
  content: QuestionContent;
  /** Form.io migration — additive, optional Form.io authoring alongside `content`. */
  formIoSchemaJson: string | null;
  /** Backend-only correct-answer data for the Form.io schema (admin-visible). */
  scoringRulesJson: string | null;
  rendererKind: FormRendererKind;
}

export interface PlacementItemRequest {
  skill: string;
  cefrLevel: string;
  content: QuestionContent;
  itemOrder: number;
  isEnabled: boolean;
  formIoSchemaJson?: string;
  scoringRulesJson?: string;
  rendererKind?: FormRendererKind;
}

export const PLACEMENT_SKILLS = ['grammar', 'vocabulary', 'reading', 'listening', 'writing', 'speaking'] as const;

export const PLACEMENT_CEFR_LEVELS = ['A1', 'A2', 'B1', 'B2'] as const;

/** Placement items must always be scorable, so the admin editor only offers question types with
 * a correct-answer concept — single_choice/gap_fill, optionally wrapped in a group. */
export const PLACEMENT_QUESTION_TYPES = ['single_choice', 'gap_fill', 'reading_group', 'listening_group'] as const;
