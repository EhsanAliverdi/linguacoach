export interface ProgressCurrentModule {
  moduleId: string;
  title: string;
  completedActivities: number;
  totalRequired: number;
  averageScore: number | null;
  latestScore: number | null;
  isReadyToComplete: boolean;
}

export interface ProgressStats {
  activitiesCompleted: number;
  totalAttempts: number;
  retryAttempts: number;
  averageScore: number | null;
  latestScore: number | null;
  bestScore: number | null;
  activitiesThisWeek: number;
  modulesCompleted: number;
  currentModuleProgress: ProgressCurrentModule | null;
}

export interface ScoreTrendPoint {
  attemptDate: string;
  score: number;
  activityTitle: string;
  moduleTitle: string | null;
  attemptNumber: number;
}

export interface ProgressSkill {
  skillKey: string;
  skillLabel: string;
  isWeak: boolean;
}

export interface ProgressSkillSection {
  skills: ProgressSkill[];
  topStrengths: string[];
  weakestSkills: string[];
}

export interface ProgressLearningFocus {
  journeySummary: string | null;
  nextRecommendedFocus: string[];
  recurringMistakes: string[];
  weakSkills: string[];
  strongSkills: string[];
}

export interface ProgressModule {
  moduleId: string;
  title: string;
  status: 'completed' | 'current' | 'upcoming';
  completedActivities: number;
  totalRequired: number;
  averageScore: number | null;
  latestScore: number | null;
  isReadyToComplete: boolean;
  completedAt: string | null;
}

export interface ProgressSummary {
  summary: ProgressStats;
  scoreTrend: ScoreTrendPoint[];
  skillProgress: ProgressSkillSection;
  learningFocus: ProgressLearningFocus | null;
  moduleProgress: ProgressModule[];
}
