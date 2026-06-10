export interface StudentListItem {
  studentProfileId: string;
  userId: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string | null;
  onboardingStatus: string;
  lifecycleStage: string;
  cefrLevel: string | null;
  careerContext: string | null;
  learningGoal: string | null;
  learningGoalDescription: string | null;
  difficultSituationsText: string | null;
  preferredSessionDurationMinutes: number | null;
  professionalExperienceLevel: number | null;
  roleFamiliarity: number | null;
  createdAt: string;
}

export interface UpdateStudentProfileRequest {
  firstName?: string | null;
  lastName?: string | null;
  displayName?: string | null;
  careerContext?: string | null;
  learningGoal?: string | null;
  learningGoalDescription?: string | null;
  difficultSituationsText?: string | null;
  preferredSessionDurationMinutes?: number | null;
  professionalExperienceLevel?: number | null;
  roleFamiliarity?: number | null;
}

export interface PromptTemplateItem {
  id: string;
  key: string;
  version: number;
  isActive: boolean;
  maxInputTokens: number | null;
  maxOutputTokens: number | null;
}

export interface PromptTemplateDetail extends PromptTemplateItem {
  content: string;
}

export interface CareerProfileItem {
  id: string;
  name: string;
}

export interface CurriculumWordItem {
  id: string;
  word: string;
  definition: string;
  exampleSentence: string;
  priority: number;
  tags: string;
}

export interface AiProviderConfigItem {
  id: string;
  featureKey: string;
  providerName: string;
  modelName: string;
  voiceName: string | null;
  fallbackProviderName: string | null;
  fallbackModelName: string | null;
  fallbackEnabled: boolean;
}

export interface ModelTestStatus {
  modelName: string;
  ok: boolean;
  latencyMs: number;
  error: string | null;
  testedAt: string; // ISO date or default(DateTime) = "0001-01-01..."
}

export interface AiProviderCatalogItem {
  providerName: string;
  models: string[];
  hasApiKey: boolean;
  modelTests: ModelTestStatus[];
}

export interface AiConfigCategoryItem {
  id: string;
  categoryKey: string;
  displayName: string;
  providerName: string | null;
  modelName: string | null;
  voiceName: string | null;
}

export interface UpdateAiCategoryRequest {
  providerName?: string | null;
  modelName?: string | null;
  voiceName?: string | null;
}

export interface AdminStudentLearningMemory {
  journeySummary: string | null;
  strongSkills: string[];
  weakSkills: string[];
  recurringMistakes: string[];
  nextRecommendedFocus: string[];
  coveredScenarioCount: number;
  skillProfile: { skillKey: string; skillLabel: string; isWeak: boolean }[];
}
