// Mandatory Import Execution Plan addendum (2026-07-15) — large-scale ZIP package upload,
// automatic manifest inspection, and the plan/approval gate every package (large or small) must
// pass through before any AI/STT/TTS/background processing begins. Mirrors the shape of
// LinguaCoach.Application.ResourceImport's ImportPackage*/ImportExecutionPlan* C# contracts.

export interface RequestImportPackageUploadResult {
  importPackageId: string;
  uploadUrl: string;
  uploadUrlExpiresAt: string;
  storageKey: string;
}

export interface ImportPackageFolderGroup {
  folderPath: string;
  fileCount: number;
  extensions: string[];
}

export interface ImportPackageManifestSummaryDto {
  importPackageId: string;
  status: string;
  isAccepted: boolean;
  rejectionReason: string | null;
  compressedSizeBytes: number;
  expandedSizeBytes: number;
  entryCount: number;
  folderGroups: ImportPackageFolderGroup[];
  distinctExtensions: string[];
  duplicateChecksumEntryCount: number;
  unsupportedEntryCount: number;
  suspiciousEntryCount: number;
}

export interface ImportExecutionPlanDetectedGroup {
  groupKey: string;
  description: string;
  fileCount: number;
  sampleRelativePaths: string[];
  proposedResourceType: string | null;
  confidence: number;
}

export interface ImportExecutionPlanVolumeEstimate {
  totalFiles: number;
  filesByExtension: Record<string, number>;
  expectedCandidateCount: number;
  expectedAudioFilesRequiringStt: number;
  estimatedAudioMinutesRequiringStt: number;
  expectedTtsCandidates: number;
  estimatedTtsCharacters: number;
  expectedImageAnalysisCount: number;
  unmatchedFileCount: number;
}

export interface ImportExecutionPlanTimeEstimate {
  estimatedDurationRangeDescription: string;
  estimatedMinMinutes: number;
  estimatedMaxMinutes: number;
  assumptions: string;
}

export interface ImportExecutionPlanCostBreakdownLine {
  category: string;
  amount: number;
}

export interface ImportExecutionPlanCostEstimate {
  expectedCost: number;
  minCost: number;
  maxCost: number;
  currency: string;
  breakdown: ImportExecutionPlanCostBreakdownLine[];
  assumptions: string[];
  providerModelAssumptions: string;
}

export interface ImportExecutionPlanDecision {
  topic: string;
  decision: string;
  reason: string;
}

/** Phase 4.2 (Part F) — real column-mapping preview for a structured (CSV/JSON/JSONL) asset,
 *  built only for inline (paste/file, non-ZIP) packages. */
export interface ImportExecutionPlanStructuredMappingPreview {
  assetRelativePath: string;
  detectedColumns: string[];
  proposedMapping: Record<string, string>;
  ignoredColumns: string[];
  expectedRecordCount: number;
  warnings: string[];
}

export interface ImportExecutionPlanEstimate {
  detectedGroups: ImportExecutionPlanDetectedGroup[];
  ambiguousGroups: string[];
  unsupportedContentNotes: string[];
  volume: ImportExecutionPlanVolumeEstimate;
  time: ImportExecutionPlanTimeEstimate;
  cost: ImportExecutionPlanCostEstimate;
  risks: string[];
  proposedDecisions: ImportExecutionPlanDecision[];
  samplingRoundsUsed: number;
  structureConfidence: number;
  structuredMappingPreviews: ImportExecutionPlanStructuredMappingPreview[];
}

export type ImportProfileStatus =
  | 'Draft' | 'AwaitingApproval' | 'Approved' | 'Rejected' | 'PausedForCostApproval'
  | 'Executing' | 'Completed' | 'Failed' | 'Cancelled' | 'Superseded';

/** Mirrors LinguaCoach.Domain.Enums.ResourceCandidateType — camelCase (JsonStringEnumConverter). */
export type ResourceCandidateType =
  | 'unknown' | 'vocabularyEntry' | 'grammarProfileEntry' | 'readingPassage'
  | 'activityTemplateCandidate' | 'writingPrompt' | 'listeningPassage' | 'speakingPrompt';

/** Every Resource type an admin may route a group to. Audio content is further restricted to
 *  'listeningPassage' by ImportPlanInstructionValidator — see isAudioGroup() in the plan
 *  component, which narrows this list per group. */
export const ROUTABLE_RESOURCE_TYPES: { value: ResourceCandidateType; label: string }[] = [
  { value: 'vocabularyEntry', label: 'Vocabulary entry' },
  { value: 'grammarProfileEntry', label: 'Grammar profile entry' },
  { value: 'readingPassage', label: 'Reading passage' },
  { value: 'activityTemplateCandidate', label: 'Activity template candidate' },
  { value: 'writingPrompt', label: 'Writing prompt' },
  { value: 'listeningPassage', label: 'Listening passage' },
  { value: 'speakingPrompt', label: 'Speaking prompt' },
];

/** Mirrors LinguaCoach.Application.ResourceImport.ResourceImportRecognizedFields.All — the only
 *  field-mapping targets execution will accept. */
export const RECOGNIZED_FIELD_MAPPING_TARGETS: string[] = [
  'word', 'lemma', 'headword', 'grammarkey', 'explanation', 'passage', 'text', 'title',
  'prompt', 'transcript', 'scenario', 'formio', 'schema', 'template',
  'cefrlevel', 'cefr', 'skill', 'subskill', 'tags', 'focustags', 'difficultyband',
];

/** Mirrors LinguaCoach.Application.ResourceImport.ImportExecutionGroupInstruction — one
 *  folder-group's editable executable instruction: whether it's included, which Resource type it
 *  routes to, and its source-column -> target-field mappings. This is the exact typed contract
 *  both the draft-update and preview endpoints consume — no separate frontend-only shape. */
export interface ImportExecutionGroupInstruction {
  groupKey: string;
  included: boolean;
  resourceType: ResourceCandidateType | null;
  fieldMappings: Record<string, string>;
  sampleRelativePaths: string[];
}

export interface ImportPlanValidationError {
  groupKey: string | null;
  message: string;
}

export interface ImportPlanPreviewRow {
  groupKey: string;
  assetRelativePath: string;
  sourceRow: Record<string, string | null>;
  predictedCandidateType: ResourceCandidateType;
  predictedCanonicalText: string;
  warnings: string[];
}

export interface ImportPlanPreviewResult {
  rows: ImportPlanPreviewRow[];
  validationErrors: ImportPlanValidationError[];
}

export interface ImportExecutionPlanDto {
  planId: string;
  importPackageId: string;
  version: number;
  status: ImportProfileStatus;
  processingMode: string | null;
  processingModeReason: string | null;
  estimate: ImportExecutionPlanEstimate;
  approvedCostCeiling: number | null;
  createdAtUtc: string;
  approvedAtUtc: string | null;
  approvedByUserId: string | null;
  rejectedAtUtc: string | null;
  rejectionReason: string | null;
  pauseReason: string | null;
  changeReason: string | null;
  /** Phase 4.4 — bumped by every draft edit; must be echoed back on the next PUT/approve or the
   *  request is rejected with 409 (another admin or process changed the plan first). */
  concurrencyStamp: string;
  /** Phase 4.4 — true only while Status is Draft or AwaitingApproval. */
  isEditable: boolean;
  /** Phase 4.4 — the plan's actual executable instructions (what PUT/preview/approve operate
   *  on) — the single source of truth for the editor's form model. */
  groupInstructions: ImportExecutionGroupInstruction[];
  /** Phase 4.4B — the package's durable running total spent so far (backend-calculated). */
  accruedCost: number;
  accruedCostCurrency: string;
  /** Phase 4.4B — approvedCostCeiling - accruedCost, or null when no ceiling is approved yet. */
  remainingCeiling: number | null;
  /** Phase 4.4B — every past ceiling amendment for this plan, oldest first. */
  ceilingAmendments: ImportCostCeilingAmendmentDto[];
}

/** Mirrors LinguaCoach.Application.ResourceImport.ImportCostCeilingAmendmentDto — one immutable,
 *  audited record of an administrator raising a paused plan's approved cost ceiling. */
export interface ImportCostCeilingAmendmentDto {
  amendmentId: string;
  previousCeiling: number;
  newCeiling: number;
  currency: string;
  reason: string;
  administratorUserId: string | null;
  createdAtUtc: string;
}

/** Mirrors LinguaCoach.Application.ResourceImport.ImportSttOperationSummaryDto — one durable STT
 *  operation ledger row, safe to display (no credentials, no full transcript text).
 *  resultReusable is true once Status is 'Succeeded' — a future retry of this exact operation
 *  reuses the result and does not call the provider or accrue cost again. */
export interface ImportSttOperationSummaryDto {
  operationId: string;
  assetFileName: string;
  assetRelativePath: string;
  providerName: string;
  modelName: string | null;
  status: 'Pending' | 'Succeeded' | 'Failed';
  attemptNumber: number;
  resultReusable: boolean;
  calculatedCost: number | null;
  currency: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  safeErrorMessage: string | null;
  /** Phase 4.4E — the asset's real, measured audio duration (never the old flat five-minute
   *  assumption) — null if not yet measured or measurement failed. */
  measuredAudioDurationSeconds: number | null;
  audioDurationMeasurementStatus: 'NotMeasured' | 'Measured' | 'Failed';
}

/** Mirrors LinguaCoach.Application.ResourceImport.ImportAiEnrichmentOperationSummaryDto — one
 *  durable AI candidate-enrichment operation ledger row, safe to display (no credentials, no raw
 *  AI response body). resultReusable is true once Status is 'Succeeded'. */
export interface ImportAiEnrichmentOperationSummaryDto {
  operationId: string;
  resourceCandidateId: string;
  sourceLabel: string;
  operationType: string;
  providerName: string;
  modelName: string | null;
  status: 'Pending' | 'Succeeded' | 'Failed';
  attemptNumber: number;
  resultReusable: boolean;
  inputTokens: number | null;
  outputTokens: number | null;
  calculatedCost: number | null;
  currency: string;
  startedAtUtc: string;
  completedAtUtc: string | null;
  safeErrorMessage: string | null;
}
