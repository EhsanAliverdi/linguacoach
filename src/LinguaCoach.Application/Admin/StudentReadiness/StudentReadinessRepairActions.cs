namespace LinguaCoach.Application.Admin.StudentReadiness;

/// <summary>
/// Static registry of every repair/backfill action Phase 20D's readiness panel can reference.
/// Only actions with <see cref="StudentReadinessRepairActionDefinitionDto.IsImplemented"/> = true
/// are actually runnable — the rest exist so the admin UI can show "Not implemented yet" with a
/// clear reason instead of silently omitting a suggested action key.
/// </summary>
public static class StudentReadinessRepairActions
{
    public const string GenerateLearningPlanIfMissing = "generate_learning_plan_if_missing";
    public const string RefillTodayLessonIfEmpty = "refill_today_lesson_if_empty";
    public const string RefillPracticeGymIfEmpty = "refill_practice_gym_if_empty";
    public const string BackfillMissingActivityMetadata = "backfill_missing_activity_metadata";
    public const string RegenerateMissingTtsForListeningIfSupported = "regenerate_missing_tts_for_listening_if_supported";
    public const string NormalizeStudentLifecycleIfSafe = "normalize_student_lifecycle_if_safe";
    public const string RefreshProgressProjectionIfSupported = "refresh_progress_projection_if_supported";
    public const string RunAllSafeRepairs = "run_all_safe_repairs";

    public static IReadOnlyList<StudentReadinessRepairActionDefinitionDto> All { get; } =
    [
        new()
        {
            ActionKey = GenerateLearningPlanIfMissing,
            DisplayName = "Generate Learning Plan",
            Description = "Calls the existing Learning Plan service to create a plan only when the student has none. Never overwrites an existing plan.",
            Category = "Learning Plan",
            RiskLevel = ReadinessRepairRiskLevel.Low,
            IsImplemented = true,
        },
        new()
        {
            ActionKey = RefillTodayLessonIfEmpty,
            DisplayName = "Refill Today lesson",
            Description = "Triggers the same on-demand Today-lesson generation a student's own visit would trigger. Reports the exact failure reason (e.g. no enabled exercise types) if it cannot succeed.",
            Category = "Today lesson",
            RiskLevel = ReadinessRepairRiskLevel.Low,
            IsImplemented = true,
        },
        new()
        {
            ActionKey = RefillPracticeGymIfEmpty,
            DisplayName = "Refill Practice Gym",
            Description = "Not implemented yet: no single-student-scoped replenishment entry point exists — the background replenishment service only runs across all active students at once. See TODO-20D-1.",
            Category = "Practice Gym",
            RiskLevel = ReadinessRepairRiskLevel.Medium,
            IsImplemented = false,
        },
        new()
        {
            ActionKey = BackfillMissingActivityMetadata,
            DisplayName = "Backfill missing activity metadata",
            Description = "Not implemented yet: no concrete, safe existing target was identified for what metadata to backfill or how during this phase's survey. See TODO-20D-2.",
            Category = "Activity content validity",
            RiskLevel = ReadinessRepairRiskLevel.Medium,
            IsImplemented = false,
        },
        new()
        {
            ActionKey = RegenerateMissingTtsForListeningIfSupported,
            DisplayName = "Regenerate missing TTS audio",
            Description = "Not implemented yet: no single-activity/single-student TTS generation entry point exists — the TTS job only runs batch-wide on a schedule. See TODO-20D-3.",
            Category = "Audio/TTS",
            RiskLevel = ReadinessRepairRiskLevel.Medium,
            IsImplemented = false,
        },
        new()
        {
            ActionKey = NormalizeStudentLifecycleIfSafe,
            DisplayName = "Normalize student lifecycle stage",
            Description = "Not implemented yet: lifecycle transitions are normally driven by dedicated flows (placement completion, onboarding); forcing a stage jump risks bypassing invariants not fully covered by this phase's survey. See TODO-20D-4.",
            Category = "Course readiness",
            RiskLevel = ReadinessRepairRiskLevel.High,
            IsImplemented = false,
        },
        new()
        {
            ActionKey = RefreshProgressProjectionIfSupported,
            DisplayName = "Refresh progress projection",
            Description = "Not applicable: progress/mastery is always computed live from the learning ledger — there is no stored projection to refresh.",
            Category = "Progress/mastery",
            RiskLevel = ReadinessRepairRiskLevel.Low,
            IsImplemented = false,
            SupportsDryRun = false,
        },
        new()
        {
            ActionKey = RunAllSafeRepairs,
            DisplayName = "Run all safe repairs",
            Description = "Runs every implemented repair action in sequence (Generate Learning Plan, Refill Today lesson) and aggregates the results.",
            Category = "General",
            RiskLevel = ReadinessRepairRiskLevel.Low,
            IsImplemented = true,
        },
    ];

    public static StudentReadinessRepairActionDefinitionDto? Find(string actionKey) =>
        All.FirstOrDefault(a => a.ActionKey == actionKey);
}
