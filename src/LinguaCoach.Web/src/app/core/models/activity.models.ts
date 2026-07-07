export type ActivityType =
  | 'writingScenario'
  | 'speakingRolePlay'
  | 'listeningComprehension'
  | 'vocabularyPractice'
  | 'pronunciationPractice'
  | 'readingTask';

export type ActivitySource = 'aiGenerated' | 'systemFallback';

export type InteractionMode =
  | 'readOnly'
  | 'freeTextEntry'
  | 'gapFill'
  | 'multipleChoice'
  | 'matchingPairs'
  | 'sentenceBuilder'
  | 'errorCorrection'
  | 'chatReply'
  | 'audioAndFreeText'
  | 'audioAndGapFill'
  | 'emailReply'
  | 'audioResponse'
  | 'multipleChoiceMulti'
  | 'readingFillInBlanks'
  | 'reorderParagraphs'
  | 'readingWritingFillInBlanks'
  | 'listeningFillInBlanks'
  | 'highlightCorrectSummary'
  | 'highlightIncorrectWords'
  | 'writeFromDictation'
  | 'summarizeSpokenText'
  | 'answerShortQuestion'
  | 'readAloud'
  | 'repeatSentence'
  | 'respondToSituation'
  | 'describeImage'
  | 'retellLecture'
  | 'summarizeGroupDiscussion';

export interface VocabPracticeItem {
  vocabularyItemId: string;
  term: string;
  prompt: string;
  hint: string;
  explanation: string;
  meaning?: string | null;
  example?: string | null;
  partOfSpeech?: string | null;
  correctAnswer?: string | null;
  options?: string[] | null;
}

export interface ActivityDto {
  activityId: string;
  activityType: ActivityType;
  source: ActivitySource;
  title: string;
  difficulty: string;
  // WritingScenario fields — null for other activity types
  situation: string | null;
  learningGoal: string | null;
  targetPhrases: string[];
  targetVocabulary: string[];
  exampleText: string | null;
  commonMistakeToAvoid: string | null;
  instructionInSourceLanguage: string | null;
  // VocabularyPractice fields — null for other activity types
  instructions: string | null;
  practiceMode: string | null;
  vocabItems: VocabPracticeItem[] | null;
  // ListeningComprehension fields. Transcript and expected answers are only in feedback.
  scenario: string | null;
  speakerRole: string | null;
  listenerRole: string | null;
  transcriptAvailableAfterSubmit: boolean | null;
  listeningQuestions: ListeningQuestion[] | null;
  responseTask: ListeningResponseTask | null;
  audioAvailable: boolean | null;
  audioUrl: string | null;
  audioContentType: string | null;
  audioDurationSeconds: number | null;
  audioUnavailableMessage: string | null;
  audioStatus: string | null;
  // SpeakingRolePlay fields — null for other activity types
  speakingScenario: string | null;
  studentRole: string | null;
  speakingListenerRole: string | null;
  speakingGoal: string | null;
  speakingPrompt: string | null;
  expectedPoints: string[] | null;
  suggestedPhrases: string[] | null;
  maxDurationSeconds: number | null;
  // Phase 2: pattern-aware fields
  interactionMode: InteractionMode | null;
  exercisePatternKey: string | null;
  contentJson?: string | null;
  // Staged learning content (module_stage_v1) — populated for activity types
  // that have been migrated; null for types not yet migrated.
  stageContent: StageContentDto | null;
  // Form.io Practice Gym pilot — student-safe schema only, never scoring rules.
  // When present, render via the Form.io renderer instead of contentJson/stageContent.
  formIoSchemaJson?: string | null;
}

// ── Staged learning content (module_stage_v1) ─────────────────────────────────

export interface LearnExample {
  phrase: string;
  meaning: string;
  note: string | null;
}

export interface LearnContentVm {
  teachingTitle: string;
  explanation: string;
  keyPoints: string[];
  examples: LearnExample[];
  strategy: string | null;
  commonMistakes: string[];
  sourceLanguageSupport: string | null;
}

export interface VocabularyExerciseData {
  items: VocabPracticeItem[];
  practiceMode: 'matching' | 'multiple_choice' | 'fill_blank' | 'type_answer' | 'review' | string;
  successChecklist?: string[] | null;
}

export interface WritingExerciseData {
  situation?: string | null;
  audience?: string | null;
  tone?: string | null;
  expectedLength?: string | null;
  prompt?: string | null;
  requiredPhrases?: string[] | null;
  targetVocabulary?: string[] | null;
  successChecklist?: string[] | null;
}

export interface PracticeContentVm {
  instructions: string;
  scenario: string | null;
  task: string | null;
  exerciseData: unknown;
}

export interface FeedbackRubricItem {
  criterion: string;
  description: string;
  weight: number;
}

export interface FeedbackPlanVm {
  evaluationCriteria: string[];
  rubric: FeedbackRubricItem[];
  feedbackFocus: string | null;
  successCriteria: string[];
}

export interface StageContentDto {
  schemaVersion: string;
  primarySkill?: string | null;
  secondarySkills?: string[] | null;
  exerciseType?: string | null;
  learn: LearnContentVm;
  practice: PracticeContentVm;
  feedbackPlan: FeedbackPlanVm;
}

export interface ListeningExerciseData {
  speakerRole: string;
  listenerRole: string;
  audioScript: string;
  transcriptAvailableAfterSubmit: boolean;
  questions: ListeningQuestion[];
  responseTask: ListeningResponseTask | null;
}

export interface VocabAnswer {
  vocabularyItemId: string;
  answer: string;
}

export interface ListeningQuestion {
  id: string;
  question: string;
  type: string;
}

export interface ListeningResponseTask {
  prompt: string;
  expectedFocus: string | null;
}

export interface ListeningAnswer {
  questionId: string;
  answer: string;
}

export type FeedbackChangeType = 'replace' | 'add' | 'remove' | 'reorder';
export type FeedbackChangeCategory = 'grammar' | 'vocabulary' | 'tone' | 'clarity' | 'structure' | 'punctuation';
export type FeedbackChangeSeverity = 'high' | 'medium' | 'low';

export interface FeedbackChangeDto {
  type: FeedbackChangeType;
  original: string | null;
  suggested: string | null;
  reason: string | null;
  category: FeedbackChangeCategory | null;
  severity: FeedbackChangeSeverity | null;
}

export interface ActivityFeedbackDto {
  attemptId: string;
  score: number | null;
  // Coach summary
  coachSummary: string | null;
  // Focus mode: true when many issues exist and list is limited to top 3-5
  focusFirst: boolean;
  // Targeted change list — primary coaching output
  changes: FeedbackChangeDto[];
  // Improved version (alias: correctedText kept for backward compat)
  correctedText: string | null;
  whatYouDidWell: string[];
  mainMistakes: string[];
  grammarIssues: string[];
  vocabularyIssues: string[];
  toneIssues: string[];
  clarityIssues: string[];
  grammarExplanation: string | null;
  toneExplanation: string | null;
  vocabularyToRemember: string[];
  miniLesson: string | null;
  nextImprovementStep: string | null;
  rewriteChallenge: string | null;
  nextPracticeSuggestion: string | null;
  feedbackInSourceLanguage: string | null;
  questionFeedback: ListeningQuestionFeedback[] | null;
  transcript: string | null;
  responseFeedback: string | null;
  // SpeakingRolePlay feedback fields
  speakingStrengths: string[] | null;
  speakingImprovements: string[] | null;
  missingExpectedPoints: string[] | null;
  suggestedImprovedResponse: string | null;
  // Pattern Evaluation Engine result (Phase 6) — null for legacy activities
  patternEvaluation: PatternEvaluationDto | null;
}

export interface ListeningQuestionFeedback {
  questionId: string;
  question: string;
  studentAnswer: string;
  expectedAnswerSummary: string;
  isCorrect: boolean;
  score: number;
  feedback: string;
}

// ── Pattern Evaluation result models (Phase 6) ────────────────────────────────

export interface PatternEvaluationItemResult {
  itemKey: string;
  studentAnswer: string | null;
  correctAnswer: string | null;
  acceptedAnswers: string[];
  isCorrect: boolean;
  score: number;
  maxScore: number;
  feedback: string | null;
}

export interface PatternCorrection {
  category: string;
  original: string | null;
  suggestion: string;
  explanation: string;
}

export interface PatternSkillImpact {
  skillKey: string;
  label: string;
  delta: number;
  evidence: string | null;
}

export interface PatternMemorySignal {
  type: string;
  key: string;
  summary: string;
  confidence: number;
}

export interface PatternEvaluationDto {
  exercisePatternKey: string | null;
  markingMode: string;
  score: number;
  maxScore: number;
  percentage: number;
  passed: boolean;
  completed: boolean;
  itemResults: PatternEvaluationItemResult[];
  coachSummary: string | null;
  corrections: PatternCorrection[];
  suggestedImprovedAnswer: string | null;
  skillImpacts: PatternSkillImpact[];
  memorySignals: PatternMemorySignal[];
}

// ── Speaking Evaluation (Phase 16F) ───────────────────────────────────────────

export interface SpeakingEvaluationDto {
  attemptId: string;
  status: 'Pending' | 'Evaluating' | 'Completed' | 'Failed' | 'Skipped' | 'NotSupported';
  feedbackText: string | null;
  suggestedImprovement: string | null;
  transcript: string | null;
  overallScore: number | null;
  fluencyScore: number | null;
  pronunciationScore: number | null;
  completenessScore: number | null;
  relevanceScore: number | null;
  completedAtUtc: string | null;
  failureReason: string | null;
  providerName: string | null;
  modelName: string | null;
}

// ── Writing Evaluation (Phase 18B) ────────────────────────────────────────────

export type EvaluationStatus = 'Pending' | 'Evaluating' | 'Completed' | 'Failed' | 'Skipped' | 'NotSupported';

export interface WritingEvaluationDto {
  attemptId: string;
  status: EvaluationStatus;
  feedbackText: string | null;
  suggestedImprovement: string | null;
  correctedText: string | null;
  overallScore: number | null;
  grammarScore: number | null;
  vocabularyScore: number | null;
  coherenceScore: number | null;
  taskCompletionScore: number | null;
  completedAtUtc: string | null;
  failureReason: string | null;
  providerName: string | null;
  modelName: string | null;
}
