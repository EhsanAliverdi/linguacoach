export interface ProgressSummarySkill {
  skillKey: string;
  skillLabel: string;
  isWeak: boolean;
  scorePercent: number;
}

export interface ProgressSummaryLearning {
  currentCefrLevel: string | null;
  placementCompletedAt: string | null;
  currentLearningPhase: string;
  totalObjectives: number;
  objectivesCompleted: number;
  objectivesMastered: number;
  objectivesInProgress: number;
  objectivesRemaining: number;
  completionPercentage: number;
  currentObjectiveKey: string | null;
  currentObjectiveSkill: string | null;
  objectivesCompletedToday: number;
}

export interface ProgressSummaryCefr {
  startingCefrLevel: string | null;
  currentCefrLevel: string | null;
  cefrImproved: boolean;
  placementDate: string | null;
  note: string | null;
}

export interface ProgressSummaryMastery {
  masteredObjectivesCount: number;
  inProgressObjectivesCount: number;
  reviewQueueCount: number;
  weakSkillsCount: number;
  weakSkillLabels: string[];
}

export interface ProgressActivityEvent {
  eventType: string;
  description: string;
  detail: string | null;
  occurredAt: string;
}

export interface ProgressSummaryFocus {
  recommendations: string[];
  recurringMistakes: string[];
  journeySummary: string | null;
}

export interface StudentProgressSummary {
  learning: ProgressSummaryLearning;
  skills: ProgressSummarySkill[];
  cefr: ProgressSummaryCefr;
  mastery: ProgressSummaryMastery;
  recentActivity: ProgressActivityEvent[];
  focus: ProgressSummaryFocus;
}
