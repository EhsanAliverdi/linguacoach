export interface WritingExerciseDto {
  scenarioTitle: string;
  scenarioDescription: string;
  instructionInSourceLanguage: string;
  targetPhrases: string[];
  targetVocabulary: string[];
}

export interface WritingFeedbackDto {
  submissionId: string;
  overallScore: number | null;
  correctedEmail: string;
  feedbackInSourceLanguage: string;
  grammarIssues: string[];
  vocabularyIssues: string[];
  toneIssues: string[];
  suggestedPhrases: string[];
  mistakesToTrack: string[];
}
