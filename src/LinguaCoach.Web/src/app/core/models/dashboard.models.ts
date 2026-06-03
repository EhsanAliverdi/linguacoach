import { LearningPathSummary } from './learning-path.models';

export interface DashboardResponse {
  studentName: string;
  careerProfile: string;
  cefrLevel: string | null;
  message: string;
  learningPath: LearningPathSummary | null;
}
