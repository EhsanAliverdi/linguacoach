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
}

export interface PlacementItemRequest {
  skill: string;
  cefrLevel: string;
  itemType: string;
  prompt: string;
  correctAnswer: string;
  readingPassage: string | null;
  listeningAudioScript: string | null;
  itemOrder: number;
  isEnabled: boolean;
}

export const PLACEMENT_SKILLS = ['grammar', 'vocabulary', 'reading', 'listening', 'writing', 'speaking'] as const;

export const PLACEMENT_CEFR_LEVELS = ['A1', 'A2', 'B1', 'B2'] as const;

export const PLACEMENT_ITEM_TYPES = ['multiple_choice', 'gap_fill'] as const;
