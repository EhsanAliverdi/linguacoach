/** Status values for a journey objective. */
export type JourneyObjectiveStatus =
  | 'Current'
  | 'Ready'
  | 'Upcoming'
  | 'Locked'
  | 'Completed'
  | 'Review'
  | 'Blocked';

export interface JourneyObjective {
  objectiveKey: string;
  title: string | null;
  skill: string;
  cefrLevel: string;
  status: JourneyObjectiveStatus;
  sequenceNumber: number;
  isReview: boolean;
  isBlocked: boolean;
  blockedByKey: string | null;
  lastEvaluatedAt: string | null;
  isMastered: boolean;
}

export interface JourneyMilestone {
  type: string;
  label: string;
  occurredAt: string | null;
}

export interface StudentJourney {
  currentCefrLevel: string;
  currentLearningPhase: string;
  totalObjectives: number;
  completionPercentage: number;
  lastCompletedAt: string | null;
  currentObjective: JourneyObjective | null;
  upcomingObjectives: JourneyObjective[];
  completedObjectives: JourneyObjective[];
  reviewObjectives: JourneyObjective[];
  milestones: JourneyMilestone[];
  planStatus: string;
}
