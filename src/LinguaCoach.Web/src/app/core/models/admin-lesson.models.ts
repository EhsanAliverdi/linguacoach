// Phase H3 — Lesson foundation. Reviewable teaching/explanation blocks generated from (or
// manually authored about) selected published Resource Bank rows — the "Learn" half of a future
// Module. Every create/generate action stages a pending-review row; nothing here creates an
// Exercise/Module row or assigns anything to a student.

import { DiagnosticIssue } from './admin-repair.models';

export interface LessonResourceLinkDto {
  linkId: string;
  resourceType: string;
  resourceId: string;
  role: string;
  snapshotTitle: string | null;
  contentFingerprint: string | null;
}

export interface LessonDto {
  id: string;
  title: string;
  body: string;
  examplesJson: string;
  commonMistakesJson: string;
  usageNotes: string | null;
  cefrLevel: string | null;
  skill: string | null;
  subskill: string | null;
  contextTagsJson: string;
  focusTagsJson: string;
  difficultyBand: number | null;
  estimatedMinutes: number | null;
  sourceMode: string;
  generationProvider: string | null;
  generationModel: string | null;
  reviewStatus: string;
  createdByUserId: string | null;
  reviewedByUserId: string | null;
  approvedAtUtc: string | null;
  rejectedAtUtc: string | null;
  rejectionReason: string | null;
  reviewNotes: string | null;
  createdAt: string;
  updatedAtUtc: string;
  links: LessonResourceLinkDto[];
  isArchived: boolean;
}

export interface LessonListResult {
  items: LessonDto[];
  totalCount: number;
}

export interface LessonResourceLinkInput {
  resourceType: string;
  resourceId: string;
  role: string;
}

export interface CreateLessonRequestBody {
  title: string;
  body: string;
  cefrLevel?: string | null;
  skill?: string | null;
  subskill?: string | null;
  contextTags?: string[] | null;
  focusTags?: string[] | null;
  examples?: string[] | null;
  commonMistakes?: string[] | null;
  usageNotes?: string | null;
  difficultyBand?: number | null;
  estimatedMinutes?: number | null;
  links?: LessonResourceLinkInput[] | null;
}

export interface UpdateLessonRequestBody {
  title: string;
  body: string;
  examples?: string[] | null;
  commonMistakes?: string[] | null;
  usageNotes?: string | null;
  cefrLevel?: string | null;
  skill?: string | null;
  subskill?: string | null;
  contextTags?: string[] | null;
  focusTags?: string[] | null;
  difficultyBand?: number | null;
  estimatedMinutes?: number | null;
}

export interface GenerateLessonFromResourcesRequestBody {
  resources: LessonResourceLinkInput[];
  title?: string | null;
  defaultCefrLevel?: string | null;
  defaultSkill?: string | null;
  defaultSubskill?: string | null;
  defaultContextTags?: string[] | null;
  defaultFocusTags?: string[] | null;
  defaultDifficultyBand?: number | null;
  notes?: string | null;
}

export interface GenerateLessonFromResourcesResult {
  lesson: LessonDto;
  reviewRoute: string;
}

// Phase J2a — AI-assisted "Generate Learn". Same request/result shape as the deterministic
// action above; a separate action, not a replacement (see backend
// IGenerateLessonFromResourcesWithAiHandler doc comment). On AI unavailability the backend
// returns a 400 with a clear error message rather than silently degrading to a deterministic
// draft — the deterministic action stays available regardless.

export const LESSON_REVIEW_STATUSES = ['NotRequired', 'PendingReview', 'Approved', 'Rejected'] as const;
export const LESSON_SOURCE_MODES = ['Manual', 'GeneratedFromResources', 'Imported'] as const;
export const LESSON_RESOURCE_ROLES = ['Primary', 'Supporting'] as const;

// ── Phase K6 — admin archive/unarchive (soft-delete) ────────────────────────────────
export interface LessonArchiveItemResult {
  id: string;
  success: boolean;
  error: string | null;
}
export interface LessonArchiveResult {
  requestedCount: number;
  succeededCount: number;
  failedCount: number;
  items: LessonArchiveItemResult[];
}

// ── Phase K8 — "Fix with AI" repair ──────────────────────────────────────────
export interface LessonRepairResult {
  item: LessonDto;
  issuesFixed: DiagnosticIssue[];
  issuesRemaining: DiagnosticIssue[];
  providerName: string | null;
  modelName: string | null;
}
