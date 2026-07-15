using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.ResourceImport;

// ── Phase 4 (2026-07-15 large-scale AI import packages), Part C — deterministic processing-mode
// decision. Chooses Direct / FullAiAssisted / SampleDriven from the already-built manifest
// (Part B) alone — no AI call, no file-content parsing. The decision and its reason are always
// shown to the admin (Status + ProcessingModeReason on ImportPackage), never applied silently. ──

public sealed record ImportProcessingModeDecision(
    ImportProcessingMode Mode,
    string Reason);

public interface IImportProcessingModeDecisionService
{
    /// <summary>Applies the deterministic decision rules to <paramref name="manifest"/> — pure
    /// function of the manifest plus configured limits, no I/O.</summary>
    ImportProcessingModeDecision Decide(ImportPackageManifest manifest);
}
