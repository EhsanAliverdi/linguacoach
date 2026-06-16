using LinguaCoach.Application.Sessions;

namespace LinguaCoach.Infrastructure.Sessions;

/// <summary>
/// Selects the best pattern key for a single session slot using available student
/// and catalog signals. Pure logic — no DB access, no AI, no side effects.
///
/// Ranking rules (highest priority first):
/// 1. Catalog gate: only Ready + enabled + SupportsTodayLesson keys are eligible.
/// 2. Skill-weakness preference: prefer candidates whose primary skill matches the
///    weakest skill in the slot's skill group.
/// 3. Repetition avoidance: among equally-ranked candidates, prefer keys that did
///    not appear in recent history.
/// 4. Deterministic tie-break: stable alphabetical order so tests are reproducible.
/// 5. Fallback: if no candidate passes the catalog gate, return the first candidate
///    with IsFallback=true and a diagnostic reason.
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
            .Select(k => new { Key = k, Score = Score(k, weakestSkill, input.RecentPatternKeys, input.AvailableCatalog) })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToList();

        var winner = ranked[0];
        var reason = BuildReason(winner.Key, weakestSkill, input.RecentPatternKeys, input.SlotPrimarySkill, input.LearningGoalContext);

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
            .Where(e => e.IsEnabled && e.IsReady && e.SupportsTodayLesson)
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
    /// Higher score = more preferred. Range roughly 0-30.
    /// </summary>
    private static int Score(
        string candidateKey,
        string weakestSkill,
        IReadOnlyList<string> recentKeys,
        IReadOnlyList<PatternCatalogEntry> catalog)
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

        // +10 if not in recent history at all.
        if (!recentKeys.Any(k => string.Equals(k, candidateKey, StringComparison.OrdinalIgnoreCase)))
            score += 10;
        else
        {
            // Partial credit: +5 if it was seen but only once and not in the last 3.
            var recentCount = recentKeys
                .Take(3)
                .Count(k => string.Equals(k, candidateKey, StringComparison.OrdinalIgnoreCase));
            if (recentCount == 0) score += 5;
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
        string? learningGoalContext)
    {
        var parts = new List<string>();

        parts.Add($"selected '{selectedKey}'");
        parts.Add($"slot-skill='{slotPrimarySkill}'");
        parts.Add($"weakest-skill='{weakestSkill}'");

        var isRecent = recentKeys.Take(3).Any(k =>
            string.Equals(k, selectedKey, StringComparison.OrdinalIgnoreCase));
        parts.Add(isRecent ? "recent=yes(lower-priority)" : "recent=no(preferred)");

        if (!string.IsNullOrWhiteSpace(learningGoalContext))
            parts.Add($"goal-context='{learningGoalContext}'");

        return string.Join("; ", parts);
    }
}
