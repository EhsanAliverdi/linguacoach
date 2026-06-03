export interface LearningModuleSummary {
  moduleId: string;
  title: string;
  description: string;
  order: number;
  completedActivities: number;
  totalActivities: number;
  isCurrent: boolean;
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
}
