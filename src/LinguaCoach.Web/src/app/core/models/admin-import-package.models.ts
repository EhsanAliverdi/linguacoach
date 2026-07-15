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
}

export type ImportProfileStatus =
  | 'Draft' | 'AwaitingApproval' | 'Approved' | 'Rejected' | 'PausedForCostApproval'
  | 'Executing' | 'Completed' | 'Failed' | 'Cancelled' | 'Superseded';

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
}
