export interface StudentListItem {
  userId: string;
  email: string;
  onboardingStatus: string;
  cefrLevel: string | null;
  createdAt: string;
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
