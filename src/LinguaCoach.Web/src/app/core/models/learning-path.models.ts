export interface LearningFocusArea {
  category: string;
  friendlyLabel: string;
  frequency?: number;
}

export interface LearningModuleSummary {
  moduleId: string;
  title: string;
  description: string;
  order: number;
  completedActivities: number;  // distinct activities (not retry count)
  totalActivities: number;
  isCurrent: boolean;
  isCompleted: boolean;
  isReadyToComplete: boolean;
  averageScore: number | null;
  latestScore: number | null;
}

export interface LearningPathSummary {
  pathId: string;
  title: string;
  modulesCompleted: number;
  totalModules: number;
  currentModule: LearningModuleSummary | null;
}

export interface LearningPathDetail extends LearningPathSummary {
  isActive: boolean;
  modules: LearningModuleSummary[];
  currentFocus: LearningFocusArea | null;
}
