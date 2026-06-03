export interface WritingScenarioDto {
  id: string;
  title: string;
  situation: string;
  learningGoal: string;
  difficulty: string;
  targetPhrases: string[];
  targetVocabulary: string[];
}

export interface WritingExerciseDto {
  scenarioTitle: string;
  scenarioDescription: string;
  learningGoal: string;
  instructionInSourceLanguage: string;
  targetPhrases: string[];
  targetVocabulary: string[];
  exampleText: string;
  commonMistakeToAvoid: string;
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
  // Teaching fields (v2)
  whatYouDidWell: string[];
  mainMistakes: string[];
  grammarExplanation: string;
  toneExplanation: string;
  vocabularyToRemember: string[];
  rewriteChallenge: string;
  nextPracticeSuggestion: string;
}
