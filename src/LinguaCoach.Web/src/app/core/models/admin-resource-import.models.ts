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
  qualityScore: number | null;
  contentFingerprint: string;
  validationStatus: string;
  reviewStatus: string;
  rejectReason: string | null;
  adminNotes: string | null;
  createdAt: string;
  updatedAtUtc: string;
}

export interface AdminResourceCandidateListResult {
  items: AdminResourceCandidateDto[];
  totalCount: number;
  overallTotalCount: number;
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

export const RESOURCE_IMPORT_MODES = ['Csv', 'Json', 'Jsonl'] as const;
export const RESOURCE_CANDIDATE_TYPES = [
  'Unknown', 'VocabularyEntry', 'GrammarProfileEntry', 'ReadingPassage', 'ActivityTemplateCandidate',
] as const;
export const RESOURCE_VALIDATION_STATUSES = ['Pending', 'Passed', 'Failed', 'NeedsReview'] as const;
export const RESOURCE_REVIEW_STATUSES = ['NotRequired', 'PendingReview', 'Approved', 'Rejected'] as const;
export const RESOURCE_IMPORT_RUN_STATUSES = [
  'Pending', 'Running', 'Completed', 'CompletedWithWarnings', 'Failed', 'Cancelled',
] as const;
