import { LearningPathSummary } from './learning-path.models';

export interface DashboardActivityStats {
  activitiesCompleted: number;
  latestScore: number | null;
  averageScore: number | null;
}

export interface DashboardFocusArea {
  category: string;
  friendlyLabel: string;
}

export interface DashboardResponse {
  studentName: string;
  careerProfile: string;
  cefrLevel: string | null;
  message: string;
  learningPath: LearningPathSummary | null;
  activityStats: DashboardActivityStats | null;
  currentFocus: DashboardFocusArea | null;
  nextRecommendedPractice: string | null;
  latestImprovement: string | null;
}
