import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface ValidationFailureItem {
  timestampUtc: string;
  patternKey: string | null;
  activityTypeName: string;
  cefrLevel: string | null;
  objectiveKey: string | null;
  validationErrors: string;
  attemptNumber: number;
}

export interface PatternFailureBreakdownItem {
  patternKey: string;
  totalFailures: number;
  abandonedCount: number;
  latestError: string | null;
}

export interface CefrFailureBreakdownItem {
  cefrLevel: string;
  totalFailures: number;
}

export interface PromptMetaItem {
  id: string;
  key: string;
  version: number;
  isActive: boolean;
  maxInputTokens: number | null;
  maxOutputTokens: number | null;
  seededAtUtc: string;
}

export interface GenerationQualitySummary {
  recentDays: number;
  validationFailureSummary: {
    totalFailures: number;
    abandonedGenerations: number;
    failuresLast24Hours: number;
  };
  latestFailures: ValidationFailureItem[];
  patternFailureBreakdown: PatternFailureBreakdownItem[];
  cefrFailureBreakdown: CefrFailureBreakdownItem[];
  promptSummary: PromptMetaItem[];
}

@Injectable({ providedIn: 'root' })
export class GenerationQualityService {
  private readonly api = environment.apiUrl;

  constructor(private http: HttpClient) {}

  getSummary(recentDays = 30): Observable<GenerationQualitySummary> {
    return this.http.get<GenerationQualitySummary>(
      `${this.api}/api/admin/generation-quality/summary?recentDays=${recentDays}`
    );
  }
}
