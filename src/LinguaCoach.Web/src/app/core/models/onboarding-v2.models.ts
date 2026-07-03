import { QuestionContent } from '../../shared/question/question-content.models';

export interface OnboardingV2Option {
  key: string;
  label: string;
}

export interface OnboardingV2ValidationMetadata {
  maxLength?: number;
  maxSelections?: number;
  minSelections?: number;
}

export interface OnboardingV2Step {
  stepKey: string;
  title: string;
  description?: string;
  stepType: string;
  requirementType: string;
  stepOrder: number;
  isEnabled: boolean;
  options?: OnboardingV2Option[];
  validationMetadata?: OnboardingV2ValidationMetadata;
  /** Unified Question-Schema (Phase 5/6) — the shared, polymorphic representation of this step,
   * for the generic step types only (null for the semantically-named one-off types). Always
   * redacted of correct-answer fields by the backend. */
  content?: QuestionContent | null;
  /** Phase 6b — which category this step belongs to, for grouping steps into visual sections. */
  categoryId?: string | null;
  categoryName?: string | null;
}

export interface OnboardingV2Status {
  flowId: string;
  currentStepKey?: string;
  steps: OnboardingV2Step[];
  completedStepKeys: string[];
  percentageComplete: number;
  isComplete: boolean;
  preliminaryCefrLevel?: string;
}

export interface SubmitStepResult {
  currentStepKey?: string;
  completedStepKeys: string[];
  percentageComplete: number;
  isComplete: boolean;
}

export interface CompleteOnboardingResult {
  success: boolean;
  preliminaryCefrLevel?: string;
}
