export interface SpeakingSessionDto {
  sessionId: string;
  scenarioTitle: string;
  scenarioGoal: string;
  maxTurns: number;
  firstAiQuestion: string;
}

export interface SpeakingTurnResultDto {
  turnNumber: number;
  aiReply: string;
  feedbackInSourceLanguage: string;
  mistakes: string[];
  grammarScore: number | null;
  vocabularyScore: number | null;
  fluencyScore: number | null;
  sessionComplete: boolean;
  overallScore: number | null;
}
