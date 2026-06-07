export type VocabularyItemStatus = 'New' | 'Practising' | 'Mastered' | 'Ignored';

export interface StudentVocabularyItem {
  id: string;
  term: string;
  suggestedPhrase: string | null;
  meaningOrExplanation: string;
  exampleSentence: string | null;
  category: string;
  status: VocabularyItemStatus;
  source: string;
  seenCount: number;
  lastSeenAtUtc: string | null;
  nextReviewAtUtc: string | null;
  createdAt: string;
  sourceActivityTitle: string | null;
  sourceModuleTitle: string | null;
}
