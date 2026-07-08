// Phase E1 — English Resource Source Registry, Import Runs, Raw Records, Candidate Staging.
// Staging only — nothing here is published to any student-facing bank table (that's Phase E4).

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
}

export interface AdminResourceCandidateListResult {
  items: AdminResourceCandidateDto[];
  totalCount: number;
  overallTotalCount: number;
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

export interface ResourceCandidateBatchAnalysisResult {
  candidatesConsidered: number;
  candidatesAnalyzed: number;
  succeededCount: number;
  failedCount: number;
  batchLimitReached: boolean;
}

export interface ResourceImportResult {
  runId: string;
  status: string;
  totalRecordCount: number;
  succeededCount: number;
  rejectedCount: number;
  warningCount: number;
  errorSummary: string | null;
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
export const RESOURCE_PUBLISH_SUPPORTED_TYPES = ['VocabularyEntry', 'GrammarProfileEntry', 'ReadingPassage'] as const;

export interface ResourceCandidatePublishResult {
  success: boolean;
  publishedEntityType: string | null;
  publishedEntityId: string | null;
  publishedAtUtc: string | null;
  errors: string[];
}

export const RESOURCE_IMPORT_MODES = ['Csv', 'Json', 'Jsonl'] as const;
export const RESOURCE_CANDIDATE_TYPES = [
  'Unknown', 'VocabularyEntry', 'GrammarProfileEntry', 'ReadingPassage', 'ActivityTemplateCandidate',
] as const;
export const RESOURCE_VALIDATION_STATUSES = ['Pending', 'Passed', 'Failed', 'NeedsReview'] as const;
export const RESOURCE_REVIEW_STATUSES = ['NotRequired', 'PendingReview', 'Approved', 'Rejected'] as const;
export const RESOURCE_IMPORT_RUN_STATUSES = [
  'Pending', 'Running', 'Completed', 'CompletedWithWarnings', 'Failed', 'Cancelled',
] as const;
