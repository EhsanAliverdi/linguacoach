namespace LinguaCoach.Application.AdminRepair;

/// <summary>
/// Phase K8 — one shared "diagnose then AI-repair" concept reused across Resource Bank, Lesson,
/// Exercise, and Module admin pages. <see cref="Code"/> is a short machine-readable id (e.g.
/// "missing_definition"); <see cref="AutoFixable"/> distinguishes issues the AI repair action can
/// actually fill in (missing descriptive/explanatory text) from issues that are flagged but
/// deliberately never AI-repaired (e.g. a missing Form.io schema or scoring rules on an Exercise
/// — correctness-critical data must never be silently AI-guessed, mirroring the same principle
/// <see cref="Infrastructure.Exercises.AiExerciseGenerationService"/> already applies to answer keys).
/// </summary>
public sealed record DiagnosticIssue(string Code, string Message, bool AutoFixable);

/// <summary>Phase K9 — aggregate result of scanning every non-archived row of one entity type and
/// AI-repairing every auto-fixable one found. Reused by Resource Bank/Lesson/Exercise/Module.</summary>
public sealed record BulkRepairResult(
    int ItemsScanned,
    int ItemsWithIssues,
    int ItemsRepaired,
    int ItemsFailed,
    IReadOnlyList<string> Errors);

/// <summary>Phase K9 — lightweight "how many rows have at least one auto-fixable issue" count for
/// a list page header, without loading full detail per row.</summary>
public sealed record IssuesSummary(int TotalItems, int ItemsWithIssues);

/// <summary>Phase K10 — one row with an auto-fixable issue, just enough (id + title) for the
/// frontend to drive a client-side "Fix All with AI" loop over the existing single-item repair
/// endpoint and show live per-item progress, instead of one opaque server-side bulk call.</summary>
public sealed record RepairableItemSummary(Guid Id, string Title);
