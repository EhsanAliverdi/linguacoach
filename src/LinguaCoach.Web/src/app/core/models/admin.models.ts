export interface AdminActivityHistoryItem {
  attemptId: string;
  activityId: string;
  activityTitle: string;
  activityType: string;
  score: number | null;
  percentage: number | null;
  passed: boolean | null;
  completed: boolean | null;
  createdAt: string;
}

export interface StudentAuditHistoryItem {
  id: string;
  source: 'AdminAuditLog' | 'StudentResetLog';
  action: string;
  actorId?: string;
  actorEmail?: string;
  timestamp: string;
  summary?: string;
  reason?: string;
  oldValue?: string;
  newValue?: string;
  correlationId?: string;
  details?: string;
}

export interface AdminStats {
  totalStudents: number;
  onboardedStudents: number;
  totalActivityAttempts: number;
}

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
  // Student-authored learning preferences (read-only for admin)
  preferredName: string | null;
  supportLanguageCode: string | null;
  supportLanguageName: string | null;
  difficultyPreference: string | null;
  translationHelpPreference: string | null;
  focusAreas: string[];
  customFocusArea: string | null;
  learningGoals: string[];
  customLearningGoal: string | null;
  learningPreferencesUpdatedAt: string | null;
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

export type StudentLifecycleStageName =
  | 'Created'
  | 'PasswordChangeRequired'
  | 'OnboardingRequired'
  | 'OnboardingInProgress'
  | 'PlacementRequired'
  | 'PlacementInProgress'
  | 'PlacementCompleted'
  | 'CourseReady'
  | 'InLesson'
  | 'ActiveLearning'
  | 'Paused'
  | 'Archived';

export interface ResetStudentRequest {
  targetStage: StudentLifecycleStageName;
  clearOnboardingAnswers: boolean;
  clearPlacementResults: boolean;
  clearCoursesAndSessions: boolean;
  clearActivityAttempts: boolean;
  clearVocabulary: boolean;
  clearLearningMemory: boolean;
  clearAudioFiles: boolean;
  clearProgressData: boolean;
  reason: string;
}

export interface ClearedItemsResult {
  onboardingAnswers: boolean;
  placementResults: boolean;
  coursesAndSessions: boolean;
  activityAttempts: boolean;
  vocabulary: boolean;
  learningMemory: boolean;
  audioFilesDeleted: number;
  progressData: boolean;
}

export interface ResetStudentResponse {
  studentId: string;
  previousStage: StudentLifecycleStageName;
  newStage: StudentLifecycleStageName;
  clearedItems: ClearedItemsResult;
  resetLogId: string;
  performedByAdminId: string;
  performedAtUtc: string;
  correlationId: string;
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
  apiEndpoint: string | null;
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

export interface CategoryTestResult {
  categoryKey: string;
  providerName: string;
  modelName: string | null;
  voiceName: string | null;
  ok: boolean;
  latencyMs: number;
  error: string | null;
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

export interface ExerciseTypeDefinition {
  key: string;
  displayName: string;
  description: string;
  primarySkill: string;
  secondarySkills: string[];
  category: string;
  isEnabled: boolean;
  implementationStatus: string;
  isAvailableForGeneration: boolean;
  rendererKey: string;
  evaluatorKey: string;
  generationPromptKey: string;
  legacyActivityType: string | null;
  exercisePatternKey: string | null;
  estimatedDurationMinutes: number;
  requiresAudio: boolean;
  requiresImage: boolean;
  supportsPracticeGym: boolean;
  supportsTodayLesson: boolean;
  minItemsPerPractice: number;
  defaultItemsPerPractice: number;
  maxItemsPerPractice: number;
  minOptionsPerItem: number;
  defaultOptionsPerItem: number;
  maxOptionsPerItem: number;
}

export interface StudentOnboardingProgressInfo {
  currentStepKey: string | null;
  completedStepKeys: string[];
  percentageComplete: number;
  startedAt: string;
  completedAt: string | null;
  isComplete: boolean;
  preliminaryCefrLevel: string | null;
}

export interface AdminStudentDetail {
  studentProfileId: string;
  userId: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  displayName: string | null;
  preferredName: string | null;
  lifecycleStage: string;
  onboardingStatus: string;
  lastCompletedStep: string | null;
  cefrLevel: string | null;
  careerContext: string | null;
  learningGoal: string | null;
  learningGoalDescription: string | null;
  difficultSituationsText: string | null;
  preferredSessionDurationMinutes: number | null;
  professionalExperienceLevel: number | null;
  roleFamiliarity: number | null;
  createdAt: string;
  archivedAt: string | null;
  supportLanguageCode: string | null;
  supportLanguageName: string | null;
  difficultyPreference: string | null;
  translationHelpPreference: string | null;
  focusAreas: string[];
  customFocusArea: string | null;
  learningGoals: string[];
  customLearningGoal: string | null;
  learningPreferencesUpdatedAt: string | null;
  onboardingProgress: StudentOnboardingProgressInfo | null;
}

export interface UpdateExerciseTypeRequest {
  isEnabled?: boolean;
  supportsPracticeGym?: boolean;
  supportsTodayLesson?: boolean;
  minItemsPerPractice?: number;
  defaultItemsPerPractice?: number;
  maxItemsPerPractice?: number;
  minOptionsPerItem?: number;
  defaultOptionsPerItem?: number;
  maxOptionsPerItem?: number;
}
