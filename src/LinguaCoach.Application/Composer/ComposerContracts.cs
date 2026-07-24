namespace LinguaCoach.Application.Composer;

/// <summary>
/// Adaptive Curriculum Sprint 5 — replaces the mechanical CEFR+tag <c>ScoreModule</c> heuristic
/// previously inline in <c>TodayPlanModuleSelectionService</c>/<c>PracticeGymModuleSelectionService</c>.
/// The caller still owns eligibility (Approved/non-archived, has an approved Lesson+Exercise, CEFR
/// match, the 14-day reuse-cooldown spacing filter) — this service only ranks/selects among an
/// already-eligible candidate pool, reasoning over goal-vector relevance and skill-graph mastery
/// gaps the caller has already resolved into deterministic per-candidate flags (never asked to
/// infer mastery/goal facts itself). Follows the AI-draft-then-validate convention used by
/// <c>SkillGraphDraftingService</c>/<c>ModuleSkillGraphTaggingService</c>: one bounded AI call,
/// retried once on bad JSON, never throws, and every ranked id is checked against the real
/// candidate set before being trusted — an AI-hallucinated id is dropped, never applied.
/// </summary>
public interface ICurriculumComposerService
{
    Task<ComposerRankingResult> RankCandidatesAsync(ComposerRankingRequest request, CancellationToken ct = default);
}

/// <summary>One eligible candidate the composer is allowed to rank — already CEFR-filtered,
/// already confirmed to have launchable content, already excluded from the recent-reuse cooldown.
/// <see cref="IsWeaknessMatch"/>/<see cref="IsGoalMatch"/> are real facts the caller computed from
/// <c>StudentMasteryEvaluationService</c>/<c>StudentGoalWeight</c> — the AI reasons over them, it
/// does not compute them.</summary>
public sealed record ComposerCandidate(
    Guid ModuleId,
    string Title,
    string? Skill,
    string? Subskill,
    string? CefrLevel,
    int? DifficultyBand,
    int? EstimatedMinutes,
    IReadOnlyList<string> ContextTags,
    IReadOnlyList<string> FocusTags,
    string? ObjectiveKey,
    /// <summary>True when this candidate's skill/node coverage overlaps a skill-graph node the
    /// student is currently Weak/AtRisk on (per <c>StudentMasteryEvaluationService.EvaluateStudentAsync</c>),
    /// or (for Practice Gym) a caller-supplied weakness signal.</summary>
    bool IsWeaknessMatch,
    /// <summary>True when this candidate's context tags overlap the student's top-weighted
    /// <c>StudentGoalWeight</c> goal tags.</summary>
    bool IsGoalMatch,
    /// <summary>True when this candidate's skill was assigned to this student within the last 3
    /// days (a tighter novelty band than the hard 14-day reuse-cooldown the caller already
    /// enforced) — a soft "you just did this skill" signal, not a hard exclusion.</summary>
    bool RecentlyPractisedSameSkill,
    /// <summary>Skill Graph pipeline audit (2026-07-24, Bug #4) — true when this candidate's
    /// skill-graph node has a prerequisite node (<c>SkillGraphPrerequisiteEdge</c>) the student is
    /// currently AtRisk on (per <c>StudentMasteryEvaluationService.EvaluateStudentAsync</c>) —
    /// a real struggle signal, not merely "not yet mastered" (a never-attempted prerequisite does
    /// NOT set this, or nothing could ever be shown to a brand-new student). A soft deprioritization
    /// signal for the composer, never a hard pool filter — see the fix's review doc for why.</summary>
    bool HasUnmetPrerequisite = false);

public sealed record ComposerRankingRequest(
    Guid StudentId,
    /// <summary>"Today" or "PracticeGym" — included in the prompt for tone/context only, has no
    /// effect on validation.</summary>
    string SurfaceName,
    IReadOnlyList<ComposerCandidate> Candidates,
    int MaxResults,
    string? RequestedSkill = null,
    string? RequestedSubskill = null,
    string? RequestedObjectiveKey = null,
    /// <summary>Today's preferred session length, in minutes — the AI reasons over this against
    /// each candidate's already-included <c>EstimatedMinutes</c>. Not set for Practice Gym, which
    /// has no session-length concept.</summary>
    int? PreferredSessionLengthMinutes = null,
    /// <summary>Practice Gym's requested difficulty band (1-5) — the AI reasons over this against
    /// each candidate's already-included <c>DifficultyBand</c>. Not set for Today.</summary>
    int? RequestedDifficulty = null);

public sealed record ComposerRankingResult(
    bool Success,
    /// <summary>Real, validated <see cref="ComposerCandidate.ModuleId"/> values from the request's
    /// candidate list, in the composer's preferred order. Empty when <see cref="Success"/> is
    /// false, or when the AI's response contained no valid, recognized candidate id.</summary>
    IReadOnlyList<Guid> RankedModuleIds,
    string? SelectionReason,
    string? FailureReason);
