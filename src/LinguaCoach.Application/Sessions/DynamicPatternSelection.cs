namespace LinguaCoach.Application.Sessions;

// ── Ledger-derived signals ─────────────────────────────────────────────────────

/// <summary>
/// Compact, pre-fetched signals derived from the StudentLearningEvent ledger.
/// Built once per session by SessionGeneratorService and passed to the selector.
/// Null = no ledger data available; selector falls back to 10A behaviour.
/// </summary>
public sealed record LedgerSignals(
    /// <summary>
    /// Pattern keys from recent ledger events, newest-first. Used for repetition avoidance.
    /// Replaces the ad-hoc SessionExercise history query where ledger data is available.
    /// </summary>
    IReadOnlyList<string> RecentPatternKeys,

    /// <summary>
    /// Pattern keys tagged NeedsReview or Failed in recent events, newest-first.
    /// Selector boosts these to surface weak-area review.
    /// </summary>
    IReadOnlyList<string> WeakPatternKeys,

    /// <summary>
    /// Pattern keys where outcome was Mastered, newest-first.
    /// Selector deprioritises these unless no alternative exists.
    /// </summary>
    IReadOnlyList<string> MasteredPatternKeys,

    /// <summary>
    /// Learning goal context sourced from the most recent ledger event that had one set.
    /// Null when no event carried a goal context.
    /// </summary>
    string? LedgerGoalContext);

// ── Selector input ─────────────────────────────────────────────────────────────

/// <summary>
/// All signals available to the dynamic pattern selector for one session slot.
/// Null/empty fields are safe — the selector degrades gracefully.
/// </summary>
public sealed record PatternSelectionInput(
    /// <summary>CEFR level string, e.g. "A2", "B1". Null = unknown.</summary>
    string? CefrLevel,

    /// <summary>Map of skill key → score 0-100. Empty = no profile.</summary>
    IReadOnlyDictionary<string, int> SkillScores,

    /// <summary>
    /// Student's declared learning goal/context, e.g. "day-to-day", "travel",
    /// "workplace", "study". Null = unset. Never assumed to be workplace.
    /// </summary>
    string? LearningGoalContext,

    /// <summary>PatternKeys used in the student's last N sessions, ordered newest-first.</summary>
    IReadOnlyList<string> RecentPatternKeys,

    /// <summary>Candidate pool for this slot — pattern keys valid for this slot.</summary>
    IReadOnlyList<string> CandidatePatternKeys,

    /// <summary>Skill this slot is primarily intended to train, e.g. "Writing".</summary>
    string SlotPrimarySkill,

    /// <summary>Available catalog entries (Ready + enabled + SupportsTodayLesson).</summary>
    IReadOnlyList<PatternCatalogEntry> AvailableCatalog,

    /// <summary>
    /// Pre-fetched ledger signals. Null = no ledger data; selector degrades to 10A behaviour.
    /// </summary>
    LedgerSignals? Ledger = null);

/// <summary>
/// Lightweight catalog entry passed to the selector — derived from ExerciseTypeRegistryEntry.
/// </summary>
public sealed record PatternCatalogEntry(
    string PatternKey,
    string PrimarySkill,
    bool IsEnabled,
    bool IsReady,
    bool SupportsTodayLesson);

// ── Selector result ────────────────────────────────────────────────────────────

/// <summary>
/// Decision produced by the dynamic pattern selector for one session slot.
/// </summary>
public sealed record PatternSelectionResult(
    /// <summary>Chosen pattern key.</summary>
    string SelectedPatternKey,

    /// <summary>Skill the chosen pattern targets.</summary>
    string TargetSkill,

    /// <summary>Human-readable reason for tracing and tests.</summary>
    string Reason,

    /// <summary>True when the selector fell back to a default because signals were missing.</summary>
    bool IsFallback);
