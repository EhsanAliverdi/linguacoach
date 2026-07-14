using LinguaCoach.Application.Sessions;

namespace LinguaCoach.Infrastructure.Sessions;

/// <summary>
/// Selects the best pattern key for a single session slot using available student
/// and catalog signals. Pure logic — no DB access, no AI, no side effects.
///
/// Ranking rules (highest priority first):
/// 1. Catalog gate: only Ready + enabled keys are eligible.
/// 2. Skill-weakness preference: prefer candidates whose primary skill matches the
///    weakest skill in the slot's skill group.
/// 3. Ledger boost: +15 if pattern appears in weak/NeedsReview/Failed ledger events.
/// 4. Ledger repetition avoidance: -8 if pattern was in last 3 ledger events.
/// 5. Mastered deprioritisation: -5 if pattern appears in mastered ledger events and
///    a non-mastered alternative exists (soft penalty, not a hard exclusion).
/// 6. Repetition avoidance (session history): prefer keys not in recent session history.
/// 7. Deterministic tie-break: stable alphabetical order so tests are reproducible.
/// 8. Fallback: if no candidate passes the catalog gate, return the first candidate
///    with IsFallback=true and a diagnostic reason.
///
/// When no ledger signals are present the selector falls back to 10A behaviour exactly.
/// </summary>
public static class DynamicPatternSelector
{
    /// <summary>
    /// Select the best pattern key from the input's candidate pool.
    ///
    /// Explicit override contract: callers that already have a hard-coded
    /// pattern= or exerciseType= must NOT pass it through this method — they
    /// should use the override directly. This method only runs when the system
    /// needs to choose automatically.
    /// </summary>
    public static PatternSelectionResult Select(PatternSelectionInput input)
    {
        // Build the set of catalog-approved keys for this slot.
        var approvedKeys = BuildApprovedKeySet(input.AvailableCatalog);

        // Filter candidates to catalog-approved only.
        var eligible = input.CandidatePatternKeys
            .Where(k => approvedKeys.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        if (eligible.Count == 0)
        {
            // Hard fallback: no approved candidate — use the first raw candidate anyway
            // so session generation does not crash. FilterUnavailableExerciseTypesAsync
            // in the generator will remove it if still unavailable.
            var fallbackKey = input.CandidatePatternKeys.FirstOrDefault() ?? string.Empty;
            return new PatternSelectionResult(
                SelectedPatternKey: fallbackKey,
                TargetSkill: input.SlotPrimarySkill,
                Reason: "fallback: no catalog-approved candidate found; using raw first candidate",
                IsFallback: true);
        }

        // Determine the weakest skill relevant to this slot.
        var weakestSkill = ResolveWeakestSkill(input.SkillScores, input.SlotPrimarySkill);

        // Score each eligible candidate.
        var ranked = eligible
            .Select(k => new { Key = k, Score = Score(k, weakestSkill, input.RecentPatternKeys, input.AvailableCatalog, input.Ledger) })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToList();

        var winner = ranked[0];
        var reason = BuildReason(winner.Key, weakestSkill, input.RecentPatternKeys, input.SlotPrimarySkill, input.LearningGoalContext, input.Ledger);

        return new PatternSelectionResult(
            SelectedPatternKey: winner.Key,
            TargetSkill: ResolveTargetSkillLabel(winner.Key, input.AvailableCatalog, input.SlotPrimarySkill),
            Reason: reason,
            IsFallback: false);
    }

    // ── Internal helpers ───────────────────────────────────────────────────────

    private static HashSet<string> BuildApprovedKeySet(IReadOnlyList<PatternCatalogEntry> catalog)
    {
        return catalog
            .Where(e => e.IsEnabled && e.IsReady)
            .Select(e => e.PatternKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns the weakest skill key for this slot, or the slot's default primary skill
    /// when no profile data exists.
    /// </summary>
    private static string ResolveWeakestSkill(
        IReadOnlyDictionary<string, int> skillScores,
        string slotPrimarySkill)
    {
        if (skillScores.Count == 0)
            return slotPrimarySkill;

        // Find the skill with the lowest score; use the slot's own skill as a tiebreaker.
        var weakest = skillScores
            .OrderBy(kv => kv.Value)
            .ThenBy(kv => string.Equals(kv.Key, slotPrimarySkill, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .First();

        return weakest.Key;
    }

    /// <summary>
    /// Higher score = more preferred. Range roughly -13 to +45.
    ///
    /// Base scoring (10A):
    ///   +20  primary skill matches weakest skill
    ///   +10  not in session recent history at all
    ///   +5   seen in history but not in last-3
    ///
    /// Ledger-aware additions (10C):
    ///   +15  pattern appears in weak/NeedsReview/Failed ledger events
    ///   -8   pattern appears in last 3 ledger events (over-repetition avoidance)
    ///   -5   pattern appears in mastered ledger events (soft deprioritisation)
    ///
    /// When ledger is null all ledger terms are 0 — identical to 10A.
    /// </summary>
    private static int Score(
        string candidateKey,
        string weakestSkill,
        IReadOnlyList<string> recentKeys,
        IReadOnlyList<PatternCatalogEntry> catalog,
        LedgerSignals? ledger)
    {
        var score = 0;

        // +20 if this candidate's primary skill matches the weakest skill.
        var entry = catalog.FirstOrDefault(e =>
            string.Equals(e.PatternKey, candidateKey, StringComparison.OrdinalIgnoreCase));

        if (entry is not null &&
            string.Equals(entry.PrimarySkill, weakestSkill, StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        // +10 if not in recent session history at all.
        if (!recentKeys.Any(k => string.Equals(k, candidateKey, StringComparison.OrdinalIgnoreCase)))
            score += 10;
        else
        {
            // Partial credit: +5 if seen but not in the last 3.
            var recentCount = recentKeys
                .Take(3)
                .Count(k => string.Equals(k, candidateKey, StringComparison.OrdinalIgnoreCase));
            if (recentCount == 0) score += 5;
        }

        // ── Ledger signals (10C) — all zero when ledger is null ──────────────
        if (ledger is not null)
        {
            // +15 if pattern is in weak/NeedsReview/Failed events — needs attention.
            if (ledger.WeakPatternKeys.Any(k =>
                    string.Equals(k, candidateKey, StringComparison.OrdinalIgnoreCase)))
                score += 15;

            // -8 if pattern appeared in the last 3 ledger events — avoid immediate repetition.
            var ledgerLast3Count = ledger.RecentPatternKeys
                .Take(3)
                .Count(k => string.Equals(k, candidateKey, StringComparison.OrdinalIgnoreCase));
            if (ledgerLast3Count > 0)
                score -= 8;

            // -5 soft penalty if mastered — deprioritise but don't exclude.
            if (ledger.MasteredPatternKeys.Any(k =>
                    string.Equals(k, candidateKey, StringComparison.OrdinalIgnoreCase)))
                score -= 5;
        }

        return score;
    }

    private static string ResolveTargetSkillLabel(
        string patternKey,
        IReadOnlyList<PatternCatalogEntry> catalog,
        string defaultSkill)
    {
        return catalog
            .FirstOrDefault(e => string.Equals(e.PatternKey, patternKey, StringComparison.OrdinalIgnoreCase))
            ?.PrimarySkill ?? defaultSkill;
    }

    private static string BuildReason(
        string selectedKey,
        string weakestSkill,
        IReadOnlyList<string> recentKeys,
        string slotPrimarySkill,
        string? learningGoalContext,
        LedgerSignals? ledger)
    {
        var parts = new List<string>();

        parts.Add($"selected '{selectedKey}'");
        parts.Add($"slot-skill='{slotPrimarySkill}'");
        parts.Add($"weakest-skill='{weakestSkill}'");

        var isRecent = recentKeys.Take(3).Any(k =>
            string.Equals(k, selectedKey, StringComparison.OrdinalIgnoreCase));
        parts.Add(isRecent ? "recent=yes(lower-priority)" : "recent=no(preferred)");

        if (ledger is not null)
        {
            var isWeak = ledger.WeakPatternKeys.Any(k =>
                string.Equals(k, selectedKey, StringComparison.OrdinalIgnoreCase));
            if (isWeak) parts.Add("ledger=weak-boosted");

            var isMastered = ledger.MasteredPatternKeys.Any(k =>
                string.Equals(k, selectedKey, StringComparison.OrdinalIgnoreCase));
            if (isMastered) parts.Add("ledger=mastered-deprioritised");

            var ledgerLast3 = ledger.RecentPatternKeys.Take(3).Any(k =>
                string.Equals(k, selectedKey, StringComparison.OrdinalIgnoreCase));
            if (ledgerLast3) parts.Add("ledger=last3-penalised");

            var goalCtx = learningGoalContext ?? ledger.LedgerGoalContext;
            if (!string.IsNullOrWhiteSpace(goalCtx))
                parts.Add($"goal-context='{goalCtx}'");
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(learningGoalContext))
                parts.Add($"goal-context='{learningGoalContext}'");
        }

        return string.Join("; ", parts);
    }
}
