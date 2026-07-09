import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PracticeGymSuggestionItem {
  readinessItemId: string;
  title: string;
  description: string;
  primarySkill: string | null;
  secondarySkills: string[];
  patternKey: string | null;
  activityType: string | null;
  targetCefrLevel: string;
  studentCefrLevelSnapshot: string | null;
  curriculumObjectiveKey: string | null;
  curriculumObjectiveTitle: string | null;
  contextTags: string[];
  focusTags: string[];
  routingReason: string;
  isLowerLevelContent: boolean;
  difficultyBand: number;
  estimatedDurationMinutes: number | null;
  supportLanguageName: string | null;
  status: string;
  callToAction: string;
  explanation: string;
  linkedLearningActivityId: string | null;
  linkedLearningSessionId: string | null;
  linkedSessionExerciseId: string | null;
}

/** Phase H7 — student-safe Learn Item projection within a Practice Gym module suggestion. */
export interface PracticeGymModuleLearnItemSummary {
  learnItemId: string;
  title: string;
  body: string;
  examples: string[];
  commonMistakes: string[];
  usageNotes: string | null;
}

/** Phase H7 — student-safe Activity Definition projection. Never carries an answer key or
 * scoring rules — those are backend-only per ActivityDefinition's own contract. */
export interface PracticeGymModuleActivitySummary {
  activityDefinitionId: string;
  title: string;
  description: string | null;
  instructions: string;
  activityType: string;
  formSchemaJson: string | null;
  estimatedMinutes: number | null;
}

export interface PracticeGymModuleSuggestion {
  moduleDefinitionId: string;
  title: string;
  description: string | null;
  cefrLevel: string | null;
  skill: string | null;
  subskill: string | null;
  difficultyBand: number | null;
  estimatedMinutes: number | null;
  contextTags: string[];
  focusTags: string[];
  reason: string;
  isReview: boolean;
  isScaffold: boolean;
  isRemediation: boolean;
  linkedLearnItemSummaries: PracticeGymModuleLearnItemSummary[];
  linkedActivitySummaries: PracticeGymModuleActivitySummary[];
  /** Phase H10 — true when this suggestion can actually be started right now. False (with
   * unsupportedReason set) for activity types/renderers not yet supported by the launch bridge. */
  canLaunch: boolean;
  unsupportedReason: string | null;
}

/** Phase H7 — additive, optional. Null when no compatible approved Module exists; the
 * readiness-pool-backed sections above remain the source of truth in that case. */
export interface PracticeGymModuleSuggestionsSection {
  suggestions: PracticeGymModuleSuggestion[];
  fallbackRequired: boolean;
  fallbackReason: string | null;
  selectionReason: string | null;
  targetCefrLevel: string | null;
  warnings: string[];
}

export interface PracticeGymSuggestionsResponse {
  suggestedItems: PracticeGymSuggestionItem[];
  continueItems: PracticeGymSuggestionItem[];
  reviewItems: PracticeGymSuggestionItem[];
  readyCount: number;
  reviewOnlyCount: number;
  reservedCount: number;
  isReplenishmentRecommended: boolean;
  generatedAtUtc: string;
  moduleSuggestions: PracticeGymModuleSuggestionsSection | null;
}

export interface StartSuggestionResult {
  success: boolean;
  failureReason: string | null;
  learningActivityId: string | null;
  learningSessionId: string | null;
  sessionExerciseId: string | null;
  alreadyReserved: boolean;
}

/** Phase H10 — result of starting a module-based suggestion. Never carries an answer key or
 * scoring rules. When success is false, unsupportedReason explains why and the student should
 * stay on the existing suggestion sections. */
export interface ModuleSuggestionStartResult {
  success: boolean;
  unsupportedReason: string | null;
  moduleDefinitionId: string;
  activityDefinitionId: string | null;
  learningActivityId: string | null;
  title: string | null;
  instructions: string | null;
  rendererType: string | null;
  formSchemaJson: string | null;
  estimatedMinutes: number | null;
  skill: string | null;
  subskill: string | null;
  cefrLevel: string | null;
  canSubmit: boolean;
  learnItem: PracticeGymModuleLearnItemSummary | null;
}

/** Student-friendly label for each routing reason. */
export function routingReasonLabel(reason: string): string {
  switch (reason?.toLowerCase()) {
    case 'normal': return 'Recommended for your current goal';
    case 'review': return 'Review';
    case 'scaffold': return 'Step back to strengthen basics';
    case 'remediation': return 'Targeted fix';
    case 'fallback': return 'General practice';
    default: return 'Practice';
  }
}

@Injectable({ providedIn: 'root' })
export class PracticeGymSuggestionsService {
  private readonly base = `${environment.apiUrl}/practice-gym`;

  constructor(private http: HttpClient) {}

  getSuggestions(): Observable<PracticeGymSuggestionsResponse> {
    return this.http.get<PracticeGymSuggestionsResponse>(`${this.base}/suggestions`);
  }

  startSuggestion(readinessItemId: string): Observable<StartSuggestionResult> {
    return this.http.post<StartSuggestionResult>(
      `${this.base}/suggestions/${readinessItemId}/start`,
      null
    );
  }

  completeSuggestion(readinessItemId: string): Observable<void> {
    return this.http.post<void>(
      `${this.base}/suggestions/${readinessItemId}/complete`,
      null
    );
  }

  /** Phase H10 — starts a module-based suggestion. On success, navigate to
   * `/activity?activityId=<learningActivityId>` — the existing Practice Gym launch/submit flow
   * handles the rest unchanged. */
  startModuleSuggestion(moduleDefinitionId: string): Observable<ModuleSuggestionStartResult> {
    return this.http.post<ModuleSuggestionStartResult>(
      `${this.base}/module-suggestions/${moduleDefinitionId}/start`,
      null
    );
  }
}
