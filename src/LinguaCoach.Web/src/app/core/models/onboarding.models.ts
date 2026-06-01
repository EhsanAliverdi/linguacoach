export type SkillFocus = 'Writing' | 'Speaking' | 'Vocabulary';

export interface OnboardingStatusResponse {
  currentStep: string;
  isComplete: boolean;
  languagePairId?: string; // null until the language step is complete
}

export interface OnboardingStepResult {
  lastCompletedStep: string;
  isComplete: boolean;
}

export interface SetLanguageRequest {
  step: 'language';
  languagePairId: string;
}

export interface SetTrackRequest {
  step: 'track';
  learningTrackId: string;
}

export interface SetCareerRequest {
  step: 'career';
  careerProfileId: string;
}

export interface SetSkillRequest {
  step: 'skill';
  skillFocus: number; // 0=Writing, 1=Speaking, 2=Vocabulary
}

export type OnboardingStepRequest =
  | SetLanguageRequest
  | SetTrackRequest
  | SetCareerRequest
  | SetSkillRequest;
