export interface DashboardSummaryProfile {
  displayName: string;
  cefrLevel: string | null;
  supportLanguage: string | null;
}

export interface DashboardSummaryCourseReadiness {
  isLearningReady: boolean;
  lifecycleStatus: string;
  placementRequired: boolean;
  learningPlanExists: boolean;
}

/** status: Ready | InProgress | Completed | Preparing | NotAvailable */
export interface DashboardSummaryTodaySession {
  status: string;
  sessionId: string | null;
  title: string | null;
  topic: string | null;
  sessionGoal: string | null;
  focusSkill: string | null;
  durationMinutes: number | null;
  exerciseCount: number | null;
  actionLabel: string;
}

export interface DashboardSummaryLearningPlan {
  pathTitle: string | null;
  currentObjective: string | null;
  currentObjectiveDescription: string | null;
  objectiveIndex: number;
  totalObjectives: number;
  modulesCompleted: number;
  remainingObjectives: number;
  completedActivities: number;
  totalActivities: number;
  progressPercent: number;
}

export interface DashboardSummaryPracticeItem {
  readinessItemId: string;
  title: string;
  description: string;
  primarySkill: string | null;
  callToAction: string;
}

/** status: Ready | Preparing | NotAvailable */
export interface DashboardSummaryPractice {
  status: string;
  suggestedItem: DashboardSummaryPracticeItem | null;
  reviewQueueCount: number;
  weakestSkill: string | null;
}

export interface DashboardSummarySkillItem {
  skillKey: string;
  skillLabel: string;
  isWeak: boolean;
  scorePercent: number;
}

export interface DashboardSummaryProgress {
  skillProfile: DashboardSummarySkillItem[];
  strongSkills: string[];
  weakSkills: string[];
  nextRecommendedFocus: string[];
  journeySummary: string | null;
  activitiesCompleted: number;
  streakDays: number;
}

export interface DashboardSummaryQuickStats {
  currentCefr: string | null;
  streakDays: number;
  activitiesCompleted: number;
  reviewQueueCount: number;
}

export interface DashboardSummaryWarnings {
  missingLearningPlan: boolean;
  missingTodaySession: boolean;
  practiceUnavailable: boolean;
  placementIncomplete: boolean;
}

export interface StudentDashboardSummary {
  profile: DashboardSummaryProfile;
  courseReadiness: DashboardSummaryCourseReadiness;
  todaySession: DashboardSummaryTodaySession;
  learningPlan: DashboardSummaryLearningPlan;
  practice: DashboardSummaryPractice;
  progress: DashboardSummaryProgress;
  quickStats: DashboardSummaryQuickStats;
  warnings: DashboardSummaryWarnings;
}
