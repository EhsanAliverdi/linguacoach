import { QuestionContent } from '../../shared/question/question-content.models';

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
}

export interface PlacementItemRequest {
  skill: string;
  cefrLevel: string;
  content: QuestionContent;
  itemOrder: number;
  isEnabled: boolean;
}

export const PLACEMENT_SKILLS = ['grammar', 'vocabulary', 'reading', 'listening', 'writing', 'speaking'] as const;

export const PLACEMENT_CEFR_LEVELS = ['A1', 'A2', 'B1', 'B2'] as const;

/** Placement items must always be scorable, so the admin editor only offers question types with
 * a correct-answer concept — single_choice/gap_fill, optionally wrapped in a group. */
export const PLACEMENT_QUESTION_TYPES = ['single_choice', 'gap_fill', 'reading_group', 'listening_group'] as const;
