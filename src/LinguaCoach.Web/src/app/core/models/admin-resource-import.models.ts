// Phase E1 — English Resource Source Registry, Import Runs, Raw Records, Candidate Staging.
// Staging only — nothing here is published to any student-facing bank table (that's Phase E4).

import { DiagnosticIssue } from './admin-repair.models';

export interface AdminResourceSourceDto {
  sourceId: string;
  name: string;
  licenseType: string;
  sourceUrl: string | null;
  usageRestrictionNotes: string | null;
  isImportApproved: boolean;
  importedAtUtc: string | null;
  languageCode: string;
  allowsStudentDisplay: boolean;
  allowsCommercialUse: boolean;
  attributionText: string | null;
  sourceVersion: string | null;
  downloadUrl: string | null;
  createdAt: string;
  updatedAtUtc: string | null;
}

export interface AdminResourceSourceListResult {
  items: AdminResourceSourceDto[];
  totalCount: number;
  overallTotalCount: number;
  approvedCount: number;
}

export interface ResourceSourceRequest {
  name: string;
  licenseType: string;
  sourceUrl: string | null;
  usageRestrictionNotes: string | null;
  languageCode: string;
  allowsStudentDisplay: boolean;
  allowsCommercialUse: boolean;
  attributionText: string | null;
  sourceVersion: string | null;
  downloadUrl: string | null;
}

export interface AdminResourceImportRunDto {
  runId: string;
  cefrResourceSourceId: string;
  sourceName: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  status: string;
  importedByUserId: string | null;
  importMode: string;
  fileName: string;
  fileHash: string;
  sourceVersion: string | null;
  parserVersion: string;
  aiModelUsed: string | null;
  totalRecordCount: number;
  succeededCount: number;
  rejectedCount: number;
  warningCount: number;
  errorSummary: string | null;
  notes: string | null;
}

export interface AdminResourceImportRunListResult {
  items: AdminResourceImportRunDto[];
  totalCount: number;
  overallTotalCount: number;
}

export interface AdminResourceRawRecordDto {
  rawRecordId: string;
  resourceImportRunId: string;
  externalRecordId: string | null;
  rawJson: string | null;
  rawText: string | null;
  rawHash: string;
  detectedLanguageCode: string;
  detectedFormat: string;
  extractionStatus: string;
  extractionWarningsJson: string | null;
  createdAt: string;
}

export interface AdminResourceRawRecordListResult {
  items: AdminResourceRawRecordDto[];
  totalCount: number;
}

export interface AdminResourceCandidateDto {
  candidateId: string;
  resourceRawRecordId: string;
  resourceImportRunId: string;
  cefrResourceSourceId: string;
  candidateType: string;
  canonicalText: string;
  normalizedJson: string;
  languageCode: string;
  cefrLevel: string | null;
  cefrConfidence: number | null;
  primarySkill: string | null;
  subskill: string | null;
  difficultyBand: number | null;
  contextTagsJson: string | null;
  focusTagsJson: string | null;
  // Phase E2 — AI-suggested classification tags/output.
  grammarTagsJson: string | null;
  vocabularyTagsJson: string | null;
  pronunciationTagsJson: string | null;
  activitySuitabilityTagsJson: string | null;
  safetyTagsJson: string | null;
  licenseTagsJson: string | null;
  qualityScore: number | null;
  contentFingerprint: string;
  /** Phase E2 — raw AI advisory analysis output, null until analyzed at least once. */
  aiAnalysisJson: string | null;
  validationStatus: string;
  reviewStatus: string;
  /** Phase E2 broadens this field's meaning to hold the most recent deterministic
   *  validation run's {"errors":[...],"warnings":[...]} JSON summary. */
  rejectReason: string | null;
  adminNotes: string | null;
  createdAt: string;
  updatedAtUtc: string;
  // Phase E4 — publish state.
  isPublished: boolean;
  publishedAtUtc: string | null;
  publishedEntityType: string | null;
  publishedEntityId: string | null;
  publishedByUserId: string | null;
  /** Phase K2 — Passed/NeedsReview (warning-only) = true, Failed/Pending (hard-blocked) = false.
   *  Advisory only — the real gate is re-checked server-side by publish/approve-and-publish. */
  canAttemptPublish: boolean;
  /** Non-null only when canAttemptPublish is false and the candidate isn't already published. */
  publishBlockReason: string | null;
}

export interface AdminResourceCandidateListResult {
  items: AdminResourceCandidateDto[];
  totalCount: number;
  overallTotalCount: number;
}

// ── Phase K2 — review-state summary + batch approve/publish actions ────────────

export interface AdminResourceCandidateReviewSummaryDto {
  totalCount: number;
  publishedCount: number;
  passedCount: number;
  needsReviewCount: number;
  blockedCount: number;
  publishableCount: number;
  /** Phase 3 — admin-rejected, regardless of ValidationStatus. */
  rejectedCount: number;
  /** Phase 3 — admin-skipped ("intentionally ignored"), regardless of ValidationStatus. */
  skippedCount: number;
  /** Phase 3 — awaiting an explicit admin decision, not yet published. */
  pendingReviewCount: number;
}

// ── Phase 3 (2026-07-15 import candidate review workflow) — skip + content editing ─────

export interface SkipCandidateRequestBody {
  reason?: string | null;
}

export interface UpdateCandidateContentRequestBody {
  canonicalText?: string | null;
  normalizedJson?: string | null;
  cefrLevel?: string | null;
  primarySkill?: string | null;
  subskill?: string | null;
  difficultyBand?: number | null;
  contextTags?: string[] | null;
  focusTags?: string[] | null;
}

export interface BatchCandidateReasonRequestBody {
  candidateIds: string[];
  reason?: string | null;
}

export interface BatchResourceCandidateActionItemResult {
  candidateId: string;
  success: boolean;
  error: string | null;
}

export interface BatchResourceCandidateActionResult {
  requestedCount: number;
  succeededCount: number;
  failedCount: number;
  alreadyPublishedCount: number;
  batchLimitReached: boolean;
  items: BatchResourceCandidateActionItemResult[];
}

// ── Phase E2 — AI analysis / rule validation trigger results ───────────────────

export interface ResourceCandidateAnalysisSummary {
  success: boolean;
  errorMessage: string | null;
  providerName: string | null;
  modelName: string | null;
}

export interface ResourceCandidateValidationResult {
  candidateId: string;
  status: string;
  errors: string[];
  warnings: string[];
  needsHumanReview: boolean;
}

export interface ResourceCandidateAnalyzeResponse {
  candidate: AdminResourceCandidateDto;
  analysis: ResourceCandidateAnalysisSummary;
  validation: ResourceCandidateValidationResult;
}

// ── Phase E3 — read-only rendered preview ───────────────────────────────────────

export interface ResourceCandidateSourceInfoDto {
  sourceId: string;
  sourceName: string;
  licenseType: string;
  sourceUrl: string | null;
  downloadUrl: string | null;
  attributionText: string | null;
  allowsStudentDisplay: boolean;
  allowsCommercialUse: boolean;
}

export interface ResourceCandidateTagsDto {
  contextTags: string[];
  focusTags: string[];
  grammarTags: string[];
  vocabularyTags: string[];
  pronunciationTags: string[];
  activitySuitabilityTags: string[];
}

export interface ResourceCandidateAiAnalysisSummaryDto {
  cefrLevel: string | null;
  cefrConfidence: number | null;
  primarySkill: string | null;
  subskill: string | null;
  difficultyBand: number | null;
  qualityScore: number | null;
  safetyTags: string[];
}

export interface ResourceCandidateRawRecordSummaryDto {
  rawRecordId: string;
  extractionStatus: string;
  excerpt: string;
}

export interface ResourceCandidateImportRunSummaryDto {
  importRunId: string;
  sourceId: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  status: string;
}

/** One flexible rendered-preview shape covering all 5 candidate types — only the fields
 *  relevant to `kind` are populated. `studentVisibleFormIoSchemaJson` is the ONLY slot ever
 *  rendered in the "what the student would see" panel for an ActivityTemplateCandidate row. */
export interface ResourceCandidateRenderedPreviewDto {
  kind: string;
  word: string | null;
  partOfSpeech: string | null;
  definition: string | null;
  example: string | null;
  grammarTitle: string | null;
  explanation: string | null;
  grammarExamples: string[] | null;
  title: string | null;
  passageText: string | null;
  wordCount: number | null;
  estimatedReadingMinutes: number | null;
  studentVisibleFormIoSchemaJson: string | null;
  // WritingPrompt (Phase J5a)
  promptText: string | null;
  genre: string | null;
  suggestedMinWords: number | null;
  // ListeningPassage (Phase J5c)
  transcript: string | null;
  hasAudio: boolean;
  // SpeakingPrompt (Phase J5d) — reuses promptText/title above
  suggestedDurationSeconds: number | null;
  fieldSummary: string[] | null;
}

export interface ResourceCandidatePreviewDto {
  candidateId: string;
  candidateType: string;
  title: string;
  languageCode: string;
  canonicalText: string;
  normalizedContent: Record<string, string | null>;
  renderedPreviewModel: ResourceCandidateRenderedPreviewDto;
  source: ResourceCandidateSourceInfoDto;
  cefrLevel: string | null;
  cefrConfidence: number | null;
  primarySkill: string | null;
  subskill: string | null;
  difficultyBand: number | null;
  tags: ResourceCandidateTagsDto;
  qualityScore: number | null;
  safetyIssues: string[];
  validationStatus: string;
  validationErrors: string[];
  validationWarnings: string[];
  reviewStatus: string;
  contentFingerprint: string;
  duplicateIndicators: string[];
  aiAnalysisSummary: ResourceCandidateAiAnalysisSummaryDto | null;
  aiAnalysisDetailsJson: string | null;
  rawRecordSummary: ResourceCandidateRawRecordSummaryDto;
  importRunSummary: ResourceCandidateImportRunSummaryDto;
  canPreview: boolean;
  previewWarnings: string[];
  adminOnlyActivityMetadataJson: string | null;
}

// ── Phase E4 — approve/reject/publish workflow ──────────────────────────────────

/** Candidate types this admin UI's Publish button will actually attempt server-side in Phase E4
 *  — ReadingPassage/ActivityTemplateCandidate/Unknown are NOT listed here (see the banner in the
 *  candidates page): ReadingPassage publishes only when the staged text is short enough to be a
 *  genuine excerpt (server-side length gate), ActivityTemplateCandidate/Unknown are deferred
 *  entirely. The Publish button stays visible for every type so the server's specific error
 *  message is always what tells the admin why, rather than this list silently hiding the action. */
export const RESOURCE_PUBLISH_SUPPORTED_TYPES =
  ['VocabularyEntry', 'GrammarProfileEntry', 'ReadingPassage', 'WritingPrompt', 'ListeningPassage', 'SpeakingPrompt'] as const;

// ── Phase J5c — real audio-file upload for ListeningPassage candidates ─────────────────────────

export interface ResourceCandidateAudioUploadResult {
  candidateId: string;
  audioContentType: string;
}

export interface ResourceCandidateAudioUrlResult {
  url: string;
  expiresAt: string;
}

export interface ResourceCandidatePublishResult {
  success: boolean;
  publishedEntityType: string | null;
  publishedEntityId: string | null;
  publishedAtUtc: string | null;
  errors: string[];
}

// ── Phase H1 — Unified Resource Bank admin read model. Aggregates the four typed published bank
// tables above into one filtered/paginated view; no physical unified table exists behind this —
// see docs/architecture/product-model-realignment-h0.md §4 (Option B). Read-only, same as the
// typed views above: mutation still only happens through Resource Candidates (E4). ──

export type UnifiedResourceBankItemType =
  'vocabulary' | 'grammar' | 'readingReference' | 'readingPassage' | 'writing' | 'listening' | 'speaking';

/** One row of the unified Resource Bank view. `linkedLearnCount`/`linkedActivityCount`/
 *  `linkedModuleCount` are always null in H1 — Learn Item/Activity/Module don't exist yet
 *  (H3/H4/H5). `sourceTable`/`detailRoute` identify which typed table/page the row actually
 *  lives in. */
export interface UnifiedResourceBankItemDto {
  id: string;
  type: UnifiedResourceBankItemType;
  title: string;
  summary: string | null;
  cefrLevel: string;
  skill: string | null;
  subskill: string | null;
  contextTags: string[];
  focusTags: string[];
  difficultyBand: number | null;
  sourceId: string | null;
  sourceName: string | null;
  contentFingerprint: string | null;
  status: string | null;
  createdAt: string;
  updatedAt: string | null;
  sourceTable: string;
  detailRoute: string | null;
  linkedLearnCount: number | null;
  linkedActivityCount: number | null;
  linkedModuleCount: number | null;
  isArchived: boolean;
}

export interface UnifiedResourceBankListResult {
  items: UnifiedResourceBankItemDto[];
  totalCount: number;
}

// ── Phase K3 — admin archive/unarchive (soft-delete) ────────────────────────────

export interface ResourceBankArchiveItemResult {
  id: string;
  success: boolean;
  error: string | null;
}

export interface ResourceBankArchiveResult {
  requestedCount: number;
  succeededCount: number;
  failedCount: number;
  items: ResourceBankArchiveItemResult[];
}

// ── Phase K5 — admin edit of a published Resource Bank item's content/metadata ─────────────────

/** The full, untruncated, type-specific field set for editing — only fields relevant to `type`
 *  are populated. Distinct from UnifiedResourceBankItemDto's lossy display Title/Summary. */
export interface ResourceBankItemEditDto {
  id: string;
  type: UnifiedResourceBankItemType;
  cefrLevel: string;
  subskill: string | null;
  difficultyBand: number | null;
  contextTags: string[];
  focusTags: string[];
  word: string | null;
  partOfSpeech: string | null;
  notes: string | null;
  grammarPoint: string | null;
  description: string | null;
  textType: string | null;
  difficultyNotes: string | null;
  referenceExcerpt: string | null;
  title: string | null;
  passageText: string | null;
  summary: string | null;
  promptText: string | null;
  genre: string | null;
  suggestedMinWords: number | null;
  transcript: string | null;
  suggestedDurationSeconds: number | null;
  imageUrl: string | null;
}

export interface UpdateResourceBankItemRequest {
  cefrLevel: string;
  subskill?: string | null;
  difficultyBand?: number | null;
  contextTags?: string[] | null;
  focusTags?: string[] | null;
  // Vocabulary
  word?: string | null;
  partOfSpeech?: string | null;
  notes?: string | null;
  // Grammar
  grammarPoint?: string | null;
  description?: string | null;
  // ReadingReference
  textType?: string | null;
  difficultyNotes?: string | null;
  referenceExcerpt?: string | null;
  // ReadingPassage / Writing / Listening / Speaking share title
  title?: string | null;
  // ReadingPassage
  passageText?: string | null;
  summary?: string | null;
  // Writing / Speaking share promptText
  promptText?: string | null;
  // Writing
  genre?: string | null;
  suggestedMinWords?: number | null;
  // Listening
  transcript?: string | null;
  // Speaking
  suggestedDurationSeconds?: number | null;
  // Speaking (Phase K20 — describe_image)
  imageUrl?: string | null;
}

// ── Phase K8 — "Fix with AI" repair ──────────────────────────────────────────
export interface ResourceBankItemRepairResult {
  item: UnifiedResourceBankItemDto;
  issuesFixed: DiagnosticIssue[];
  issuesRemaining: DiagnosticIssue[];
  providerName: string | null;
  modelName: string | null;
}

export const UNIFIED_RESOURCE_BANK_TYPES: { value: UnifiedResourceBankItemType; label: string }[] = [
  { value: 'vocabulary', label: 'Vocabulary' },
  { value: 'grammar', label: 'Grammar' },
  { value: 'readingReference', label: 'Reading reference' },
  { value: 'readingPassage', label: 'Reading passage' },
  { value: 'writing', label: 'Writing' },
  { value: 'listening', label: 'Listening' },
  { value: 'speaking', label: 'Speaking' },
];

export const RESOURCE_BANK_CEFR_LEVELS = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'] as const;

export const RESOURCE_IMPORT_MODES = ['Csv', 'Json', 'Jsonl'] as const;
export const RESOURCE_CANDIDATE_TYPES = [
  'Unknown', 'VocabularyEntry', 'GrammarProfileEntry', 'ReadingPassage', 'ActivityTemplateCandidate',
  'WritingPrompt', 'ListeningPassage', 'SpeakingPrompt',
] as const;
export const RESOURCE_VALIDATION_STATUSES = ['Pending', 'Passed', 'Failed', 'NeedsReview'] as const;
export const RESOURCE_REVIEW_STATUSES = ['NotRequired', 'PendingReview', 'Approved', 'Rejected', 'Skipped'] as const;
export const RESOURCE_IMPORT_RUN_STATUSES = [
  'Pending', 'Running', 'Completed', 'CompletedWithWarnings', 'Failed', 'Cancelled',
] as const;
