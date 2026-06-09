export type PlacementStatusValue = 'NotStarted' | 'InProgress' | 'Completed';

export interface PlacementQuestion {
  key: string;
  prompt: string;
  type: 'rating' | 'choice' | 'text';
  options?: string[];
}

export interface PlacementSection {
  key: string;
  order: number;
  title: string;
  instructions: string;
  sectionType: 'self_check' | 'mcq' | 'reading' | 'listening' | 'writing' | 'speaking';
  scored: boolean;
  questions: PlacementQuestion[];
  passage?: string | null;
  audioScript?: string | null;
  writingPrompt?: string | null;
  speakingPrompt?: string | null;
}

export interface PlacementStatus {
  status: PlacementStatusValue;
  currentSectionKey: string;
  currentSectionOrder: number;
  totalSections: number;
  lifecycleStage: string;
  isCompleted: boolean;
}

export interface PlacementCurrentSection {
  status: PlacementStatusValue;
  section: PlacementSection | null;
  currentSectionOrder: number;
  totalSections: number;
  isCompleted: boolean;
  /** Relative API URL for server-side listening audio. Only set for the listening section. */
  audioUrl?: string | null;
  /** True when server-side TTS audio is available and ready to play. */
  audioAvailable?: boolean;
}

export interface PlacementAnswerInput {
  questionKey: string;
  responseText?: string | null;
  selectedOption?: string | null;
}

export interface SavePlacementAnswers {
  sectionKey: string;
  answers: PlacementAnswerInput[];
}

export interface PlacementSkillLevel {
  skill: string;
  level: string;
}

export interface PlacementResult {
  estimatedOverallLevel: string;
  skillLevels: PlacementSkillLevel[];
  strengths: string[];
  weaknesses: string[];
  recommendedStartingCourse?: string | null;
  recommendedSessionDuration?: number | null;
  placementNotes?: string | null;
  isCompleted: boolean;
}
