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
