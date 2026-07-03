import { QuestionContent } from '../../shared/question/question-content.models';

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
  content?: QuestionContent | null;
  categoryId?: string | null;
}

export interface AdminOnboardingCategoryDto {
  categoryId: string;
  name: string;
  description: string | null;
  categoryOrder: number;
  isEnabled: boolean;
}

export interface AdminOnboardingFlowDto {
  flowId: string;
  name: string;
  version: number;
  isActive: boolean;
  steps: AdminOnboardingStepDto[];
  categories?: AdminOnboardingCategoryDto[];
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
  categoryId?: string | null;
  content?: QuestionContent | null;
}

export interface CategoryRequest {
  name: string;
  description: string | null;
  categoryOrder: number;
  isEnabled: boolean;
}

// Unified Question-Schema Phase 6b: onboarding step types are generic — the shared schema
// (SingleChoice/MultipleChoice/FreeText) plus AnswerMapping carries all the semantics that used
// to require a dedicated step type per profile field.
export const STEP_TYPES = [
  'Welcome',
  'SingleChoice',
  'MultipleChoice',
  'FreeText',
  'Summary',
] as const;

export const REQUIREMENT_TYPES = ['SystemRequired', 'AdminConfigured'] as const;

export const ANSWER_MAPPINGS = [
  'None',
  'PreferredName',
  'SupportLanguage',
  'LearningGoals',
  'CustomLearningGoal',
  'FocusAreas',
  'CustomFocusArea',
  'DifficultyPreference',
  'CareerContext',
  'SessionDuration',
  'ProfessionalExperienceLevel',
  'RoleFamiliarity',
  'LearningGoalDescription',
] as const;
