export type SkillFocus = 'Writing' | 'Speaking' | 'Vocabulary' | 'Listening';

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

/** Legacy path: select from a predefined career profile by ID. */
export interface SetCareerRequest {
  step: 'career';
  careerProfileId: string;
}

/** Free-text career path: any job/field description in any language. */
export interface SetCareerContextRequest {
  step: 'career';
  careerContext: string;
}

/** Skill step with optional student-authored learning goal (any language). */
export interface SetSkillRequest {
  step: 'skill';
  skillFocus: number; // 0=Writing, 1=Speaking, 2=Vocabulary, 3=Listening
  learningGoalDescription?: string;
  difficultSituationsText?: string;
}

export type OnboardingStepRequest =
  | SetLanguageRequest
  | SetTrackRequest
  | SetCareerRequest
  | SetCareerContextRequest
  | SetSkillRequest;
