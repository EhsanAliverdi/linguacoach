export interface AdminOnboardingFlowSummary {
  flowId: string;
  name: string;
  version: number;
  isActive: boolean;
  totalSteps: number;
  requiredSteps: number;
  createdAt: string;
}

export interface AdminOnboardingOptionDto {
  key: string;
  label: string;
}

export interface AdminOnboardingStepDto {
  stepKey: string;
  title: string;
  description: string | null;
  stepType: string;
  requirementType: string;
  answerMapping: string;
  stepOrder: number;
  isEnabled: boolean;
  options: AdminOnboardingOptionDto[] | null;
}

export interface AdminOnboardingFlowDto {
  flowId: string;
  name: string;
  version: number;
  isActive: boolean;
  steps: AdminOnboardingStepDto[];
}

export interface CreateFlowRequest {
  name: string;
  version: number;
}

export interface StepRequest {
  stepKey: string;
  title: string;
  description: string | null;
  stepType: string;
  requirementType: string;
  answerMapping: string;
  stepOrder: number;
  isEnabled: boolean;
  options: AdminOnboardingOptionDto[] | null;
}

export const STEP_TYPES = [
  'Welcome',
  'PreferredName',
  'SupportLanguage',
  'LearningGoals',
  'FocusAreas',
  'DifficultyPreference',
  'SingleChoice',
  'MultipleChoice',
  'FreeText',
  'AssessmentQuestion',
  'Summary',
  'WorkExperience',
  'SessionDuration',
] as const;

export const REQUIREMENT_TYPES = ['SystemRequired', 'AdminConfigured'] as const;

export const ANSWER_MAPPINGS = [
  'None',
  'PreferredName',
  'SupportLanguage',
  'LearningGoals',
  'FocusAreas',
  'DifficultyPreference',
  'CareerContext',
  'SessionDuration',
  'WorkExperience',
  'LearningGoalDescription',
] as const;
