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

// ── Phase E5 — Published bank browsing/search/admin management. Read-only: browse/search only,
// no edit or delete actions — all mutation still happens through Resource Candidates (E4). ──

export interface ResourceBankSourceInfoDto {
  sourceId: string;
  sourceName: string;
  licenseType: string;
  sourceUrl: string | null;
  downloadUrl: string | null;
  attributionText: string | null;
  allowsStudentDisplay: boolean;
  allowsCommercialUse: boolean;
}

/** Reverse-lookup traceability back to the originating ResourceCandidate/ImportRun. None of the
 *  Cefr* bank entities carries a forward reference to the candidate that published it, so this is
 *  built by searching ResourceCandidate for a row whose PublishedEntityType/PublishedEntityId
 *  match. `traceabilityAvailable` is false (all other fields null) when no such candidate is
 *  found — e.g. a bank row seeded some other way than through the publish workflow. */
export interface ResourceBankTraceabilityDto {
  traceabilityAvailable: boolean;
  candidateId: string | null;
  resourceImportRunId: string | null;
  contentFingerprint: string | null;
  qualityScore: number | null;
  candidateCreatedAt: string | null;
  publishedAtUtc: string | null;
  publishedByUserId: string | null;
}

export interface ResourceBankVocabularyListItemDto {
  id: string;
  word: string;
  cefrLevel: string;
  partOfSpeech: string | null;
  notes: string | null;
  sourceId: string;
  sourceName: string;
  createdAt: string;
}

export interface ResourceBankVocabularyDetailDto {
  id: string;
  word: string;
  cefrLevel: string;
  partOfSpeech: string | null;
  notes: string | null;
  createdAt: string;
  source: ResourceBankSourceInfoDto;
  traceability: ResourceBankTraceabilityDto;
}

export interface ResourceBankVocabularyListResult {
  items: ResourceBankVocabularyListItemDto[];
  totalCount: number;
}

export interface ResourceBankGrammarListItemDto {
  id: string;
  grammarPoint: string;
  cefrLevel: string;
  description: string | null;
  sourceId: string;
  sourceName: string;
  createdAt: string;
}

export interface ResourceBankGrammarDetailDto {
  id: string;
  grammarPoint: string;
  cefrLevel: string;
  description: string | null;
  createdAt: string;
  source: ResourceBankSourceInfoDto;
  traceability: ResourceBankTraceabilityDto;
}

export interface ResourceBankGrammarListResult {
  items: ResourceBankGrammarListItemDto[];
  totalCount: number;
}

export interface ResourceBankReadingReferenceListItemDto {
  id: string;
  cefrLevel: string;
  textType: string | null;
  difficultyNotes: string | null;
  referenceExcerpt: string | null;
  sourceId: string;
  sourceName: string;
  createdAt: string;
}

export interface ResourceBankReadingReferenceDetailDto {
  id: string;
  cefrLevel: string;
  textType: string | null;
  difficultyNotes: string | null;
  referenceExcerpt: string | null;
  createdAt: string;
  source: ResourceBankSourceInfoDto;
  traceability: ResourceBankTraceabilityDto;
}

export interface ResourceBankReadingReferenceListResult {
  items: ResourceBankReadingReferenceListItemDto[];
  totalCount: number;
}

export interface ResourceBankReadingPassageListItemDto {
  id: string;
  title: string;
  cefrLevel: string;
  wordCount: number;
  estimatedReadingMinutes: number;
  subskill: string | null;
  sourceId: string;
  sourceName: string;
  createdAt: string;
}

export interface ResourceBankReadingPassageDetailDto {
  id: string;
  title: string;
  passageText: string;
  summary: string | null;
  cefrLevel: string;
  difficultyBand: number | null;
  primarySkill: string;
  subskill: string | null;
  topicTags: string[];
  contextTags: string[];
  focusTags: string[];
  wordCount: number;
  estimatedReadingMinutes: number;
  attributionText: string | null;
  qualityScore: number | null;
  createdAt: string;
  source: ResourceBankSourceInfoDto;
  traceability: ResourceBankTraceabilityDto;
}

export interface ResourceBankReadingPassageListResult {
  items: ResourceBankReadingPassageListItemDto[];
  totalCount: number;
}

// ── Phase H1 — Unified Resource Bank admin read model. Aggregates the four typed published bank
// tables above into one filtered/paginated view; no physical unified table exists behind this —
// see docs/architecture/product-model-realignment-h0.md §4 (Option B). Read-only, same as the
// typed views above: mutation still only happens through Resource Candidates (E4). ──

export type UnifiedResourceBankItemType = 'vocabulary' | 'grammar' | 'readingReference' | 'readingPassage';

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
}

export interface UnifiedResourceBankListResult {
  items: UnifiedResourceBankItemDto[];
  totalCount: number;
}

export const UNIFIED_RESOURCE_BANK_TYPES: { value: UnifiedResourceBankItemType; label: string }[] = [
  { value: 'vocabulary', label: 'Vocabulary' },
  { value: 'grammar', label: 'Grammar' },
  { value: 'readingReference', label: 'Reading reference' },
  { value: 'readingPassage', label: 'Reading passage' },
];

export const RESOURCE_BANK_CEFR_LEVELS = ['A1', 'A2', 'B1', 'B2', 'C1', 'C2'] as const;

// ── Phase H2 — Import Content UX v1. A product-friendly wrapper around the Phase E1 import
// pipeline above: paste text/CSV/JSON, choose a broad resource type + default metadata, get
// back pending ResourceCandidate rows. No AI structure detection — see the "Coming soon" types
// below. Never publishes anything — review still happens on the Resource Candidates page. ──

export type ContentImportResourceType = 'vocabulary' | 'grammar' | 'reading';
export type ContentImportInputMode = 'pasted_text' | 'csv_text' | 'json_text';

/** Only these three are implemented in H2 — ResourceCandidateType has no Listening/Speaking/
 *  Writing/Mixed shape yet (see docs/architecture/product-model-realignment-h0.md). */
export const CONTENT_IMPORT_RESOURCE_TYPES: { value: ContentImportResourceType; label: string }[] = [
  { value: 'vocabulary', label: 'Vocabulary' },
  { value: 'grammar', label: 'Grammar' },
  { value: 'reading', label: 'Reading' },
];

export const CONTENT_IMPORT_COMING_SOON_TYPES = ['Listening', 'Speaking', 'Writing', 'Mixed / AI detect'];

export const CONTENT_IMPORT_INPUT_MODES: { value: ContentImportInputMode; label: string; hint: string }[] = [
  { value: 'pasted_text', label: 'Pasted text (one item per line)', hint: 'Each non-empty line becomes one candidate.' },
  { value: 'csv_text', label: 'CSV', hint: 'Paste CSV with a header row, same columns the file-upload import accepts.' },
  { value: 'json_text', label: 'JSON', hint: 'Paste a JSON array of row objects.' },
];

export interface ContentImportRequestBody {
  sourceName: string;
  resourceType: ContentImportResourceType;
  inputMode: ContentImportInputMode;
  content: string;
  defaultCefrLevel?: string | null;
  defaultSkill?: string | null;
  defaultSubskill?: string | null;
  defaultContextTags?: string[] | null;
  defaultFocusTags?: string[] | null;
  defaultDifficultyBand?: number | null;
  notes?: string | null;
}

export interface ContentImportResult {
  importRunId: string;
  sourceId: string;
  rawRecordCount: number;
  candidateCount: number;
  warningCount: number;
  status: string;
  errorSummary: string | null;
  reviewRoute: string;
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
