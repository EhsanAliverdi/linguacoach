using LinguaCoach.Application.Curriculum;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Curriculum;

/// <summary>
/// Routes generation requests to suitable curriculum objectives and CEFR bands.
///
/// Level normalization:
///   B2+ → B2 (for routing only — StudentProfile.CefrLevel is not modified)
///   Unknown/null → A1 (conservative safe fallback)
///
/// Context rules:
///   - Workplace context only when ResolvedLearningGoalContext.WorkplaceSpecific = true.
///   - No objective match → fall back to general_english at the student's band.
///   - Lower-level content requires AllowReviewOrScaffold = true and is always labelled.
///
/// DifficultyBand mapping:
///   Gentle = band 1-2 preference, Challenging = band 4-5 preference, Balanced = band 2-3.
/// </summary>
public sealed class CurriculumRoutingService : ICurriculumRoutingService
{
    private readonly ICurriculumSyllabusQuery _syllabusQuery;
    private readonly ILogger<CurriculumRoutingService> _logger;

    private static readonly string[] CoreCefrLevels =
        [CefrLevelConstants.A1, CefrLevelConstants.A2, CefrLevelConstants.B1,
         CefrLevelConstants.B2, CefrLevelConstants.C1, CefrLevelConstants.C2];

    public CurriculumRoutingService(
        ICurriculumSyllabusQuery syllabusQuery,
        ILogger<CurriculumRoutingService> logger)
    {
        _syllabusQuery = syllabusQuery;
        _logger = logger;
    }

    public string NormalizeCefrLevel(string? rawLevel)
    {
        if (string.IsNullOrWhiteSpace(rawLevel))
            return CefrLevelConstants.A1;

        // Strip plus or minus suffixes: B2+ → B2, B2- → B2, C1+ → C1
        var trimmed = rawLevel.Trim().ToUpperInvariant();
        var core = trimmed.TrimEnd('+', '-', '*');

        if (CefrLevelConstants.IsValid(core))
            return core;

        // Handle cases like "B2PLUS", "B2PLUS" written out
        foreach (var level in CoreCefrLevels)
        {
            if (trimmed.StartsWith(level, StringComparison.OrdinalIgnoreCase))
                return level;
        }

        _logger.LogWarning("CurriculumRoutingService: unrecognised CEFR level '{RawLevel}', defaulting to A1.", rawLevel);
        return CefrLevelConstants.A1;
    }

    public async Task<CurriculumRoutingRecommendation> RecommendAsync(
        CurriculumRoutingRequest request,
        CancellationToken ct = default)
    {
        var normalizedLevel = NormalizeCefrLevel(request.CurrentCefrLevel);
        var contextTags = CurriculumContextMapper.MapFromResolvedContext(request.ResolvedLearningGoalContext);
        var focusAreas = ResolveFocusAreas(request);
        var preferredBand = ResolveDifficultyBand(request.DifficultyPreference);

        // Step 1: get exact-level candidates filtered by context tags.
        var candidates = await _syllabusQuery.GetCandidatesForStudentAsync(
            normalizedLevel, contextTags, focusAreas, ct);

        // Step 2: filter by primary skill if requested.
        if (!string.IsNullOrWhiteSpace(request.PrimarySkill))
        {
            var skillFiltered = candidates
                .Where(o => string.Equals(o.PrimarySkill, request.PrimarySkill, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (skillFiltered.Count > 0)
                candidates = skillFiltered;
        }

        // Step 2a: exclude objectives whose primary skill has no runnable exercise format.
        candidates = FilterNonRunnable(candidates, request.Source);

        // Step 2b (mastery exclusion) was removed in Adaptive Curriculum Sprint 5 — see the removed
        // FilterByMastered method's former doc comment / docs/reviews/2026-07-20-adaptive-curriculum-sprint5-ai-composer-review.md
        // for why Sprint 4 made it permanently dead code.

        // Step 2c: if caller supplied a preferred objective key from the learning plan,
        //          attempt to use it before falling through to score-based selection.
        if (!string.IsNullOrWhiteSpace(request.PreferredObjectiveKey))
        {
            var preferred = await TrySelectPreferredObjectiveAsync(
                request, normalizedLevel, contextTags, focusAreas, candidates, ct);
            if (preferred is not null)
            {
                _logger.LogInformation(
                    "CurriculumRoutingService: learning-plan routing ObjectiveKey={Key} Source={Source}",
                    preferred.Key, request.Source);
                return BuildRecommendation(request, normalizedLevel, preferred, preferredBand,
                    contextTags, RoutingReason.LearningPlan, isLower: false);
            }
        }

        // Step 3: pick best exact-level candidate.
        if (candidates.Count > 0)
        {
            var best = SelectBestCandidate(candidates, preferredBand);
            _logger.LogInformation(
                "CurriculumRoutingService: normal routing Level={Level} ObjectiveKey={Key} Source={Source}",
                normalizedLevel, best.Key, request.Source);

            return BuildRecommendation(request, normalizedLevel, best, preferredBand,
                contextTags, RoutingReason.Normal, isLower: false);
        }

        // Step 4: no exact-level match. Try lower-level only if allowed.
        if (request.AllowReviewOrScaffold)
        {
            var lowerLevel = GetOneLevelDown(normalizedLevel);
            if (lowerLevel is not null)
            {
                var lowerCandidates = await _syllabusQuery.GetCandidatesForStudentAsync(
                    lowerLevel, contextTags, focusAreas, ct);

                if (!string.IsNullOrWhiteSpace(request.PrimarySkill))
                {
                    var skillFiltered = lowerCandidates
                        .Where(o => string.Equals(o.PrimarySkill, request.PrimarySkill, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (skillFiltered.Count > 0)
                        lowerCandidates = skillFiltered;
                }

                // Apply non-runnable filter to lower-level candidates as well.
                lowerCandidates = FilterNonRunnable(lowerCandidates, request.Source);

                if (lowerCandidates.Count > 0)
                {
                    var best = SelectBestCandidate(lowerCandidates, preferredBand);
                    _logger.LogInformation(
                        "CurriculumRoutingService: review/scaffold routing Level={Level}→{LowerLevel} ObjectiveKey={Key} Source={Source}",
                        normalizedLevel, lowerLevel, best.Key, request.Source);

                    return BuildRecommendation(request, normalizedLevel, best, preferredBand,
                        contextTags, RoutingReason.Review, isLower: true, lowerLevel);
                }
            }
        }

        // Step 5: fallback — general_english at the student's level, no specific objective.
        _logger.LogInformation(
            "CurriculumRoutingService: fallback routing Level={Level} Source={Source} ContextTags={Tags}",
            normalizedLevel, request.Source, string.Join(",", contextTags));

        return BuildFallback(request, normalizedLevel, contextTags, preferredBand);
    }

    /// <summary>
    /// Validates and returns the preferred objective when it passes all safety checks.
    /// Returns null — with a logged rejection reason — when the objective is not safe to use.
    ///
    /// Acceptance rules (all must pass):
    ///   1. Objective exists in the syllabus (fetched by key).
    ///   2. CEFR matches the student's normalized level, OR is one level lower with AllowReviewOrScaffold.
    ///      A lower-level preferred key is never silently accepted without AllowReviewOrScaffold.
    ///   3. If PrimarySkill is set on the request, the objective's skill must match.
    ///   4. Objective must be runnable (ActivityCompatibilityConstants.IsRunnable).
    /// (A former rule 5, mastery exclusion, was removed in Adaptive Curriculum Sprint 5 — Sprint 4
    /// made it permanently dead code. See the removed FilterByMastered method's former doc comment.)
    /// </summary>
    private async Task<CurriculumObjective?> TrySelectPreferredObjectiveAsync(
        CurriculumRoutingRequest request,
        string normalizedLevel,
        IReadOnlyList<string> contextTags,
        IReadOnlyList<string> focusAreas,
        IReadOnlyList<CurriculumObjective> currentCandidates,
        CancellationToken ct)
    {
        var key = request.PreferredObjectiveKey!;

        var objective = await _syllabusQuery.GetByKeyAsync(key, ct);
        if (objective is null)
        {
            _logger.LogDebug(
                "CurriculumRouting: preferred key '{Key}' not found in syllabus — falling back. Source={Source}",
                key, request.Source);
            return null;
        }

        // Rule 2: CEFR check — exact match or lower-level with review scaffold allowed.
        var objectiveLevel = objective.CefrLevel;
        if (!string.Equals(objectiveLevel, normalizedLevel, StringComparison.OrdinalIgnoreCase))
        {
            // Only accept lower-level when review/scaffold is explicitly enabled.
            if (!request.AllowReviewOrScaffold)
            {
                _logger.LogDebug(
                    "CurriculumRouting: preferred key '{Key}' CEFR={ObjectiveLevel} != student {StudentLevel} and AllowReviewOrScaffold=false — rejecting. Source={Source}",
                    key, objectiveLevel, normalizedLevel, request.Source);
                return null;
            }

            // Confirm it's exactly one level lower (not two or more levels down).
            var lowerLevel = GetOneLevelDown(normalizedLevel);
            if (!string.Equals(objectiveLevel, lowerLevel, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "CurriculumRouting: preferred key '{Key}' CEFR={ObjectiveLevel} is more than one level below student {StudentLevel} — rejecting. Source={Source}",
                    key, objectiveLevel, normalizedLevel, request.Source);
                return null;
            }
        }

        // Rule 3: Skill compatibility — only enforce when caller explicitly requested a skill.
        if (!string.IsNullOrWhiteSpace(request.PrimarySkill)
            && !string.Equals(objective.PrimarySkill, request.PrimarySkill, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "CurriculumRouting: preferred key '{Key}' skill={ObjectiveSkill} != requested skill {RequestedSkill} — rejecting. Source={Source}",
                key, objective.PrimarySkill, request.PrimarySkill, request.Source);
            return null;
        }

        // Rule 4: Runnable check.
        if (!ActivityCompatibilityConstants.IsRunnable(objective.PrimarySkill))
        {
            _logger.LogDebug(
                "CurriculumRouting: preferred key '{Key}' skill={ObjectiveSkill} is not runnable — rejecting. Source={Source}",
                key, objective.PrimarySkill, request.Source);
            return null;
        }

        // Rule 5 (mastery exclusion) was removed in Adaptive Curriculum Sprint 5 — Sprint 4 made
        // it permanently dead: request.MasteredObjectiveKeys is now sourced from
        // StudentMasteryEvaluationService.EvaluateStudentAsync's resolved SkillGraphNode keys, an
        // unrelated key space to CurriculumObjective.Key, so the comparison could never match a
        // real candidate. See docs/reviews/2026-07-20-adaptive-curriculum-sprint5-ai-composer-review.md.
        // CurriculumRoutingService/CurriculumObjective themselves remain live — LearningPlanService's
        // plan-generation sequencing still depends on RecommendAsync — only this proven-dead branch
        // was deleted.
        return objective;
    }

    private IReadOnlyList<CurriculumObjective> FilterNonRunnable(
        IReadOnlyList<CurriculumObjective> candidates,
        string source)
    {
        var runnable = candidates
            .Where(o => ActivityCompatibilityConstants.IsRunnable(o.PrimarySkill))
            .ToList();

        if (runnable.Count < candidates.Count)
        {
            var skipped = candidates.Count - runnable.Count;
            _logger.LogDebug(
                "CurriculumRouting: filtered {Skipped} non-runnable objective(s) from candidates. Source={Source}",
                skipped, source);
        }

        return runnable;
    }

    private static CurriculumObjective SelectBestCandidate(
        IReadOnlyList<CurriculumObjective> candidates,
        int preferredBand)
    {
        // Prefer candidates whose DifficultyBand is closest to the preferred band.
        return candidates
            .OrderBy(o => Math.Abs(o.DifficultyBand - preferredBand))
            .ThenBy(o => o.RecommendedOrder)
            .First();
    }

    private static CurriculumRoutingRecommendation BuildRecommendation(
        CurriculumRoutingRequest request,
        string normalizedLevel,
        CurriculumObjective objective,
        int difficultyBand,
        IReadOnlyList<string> contextTags,
        RoutingReason reason,
        bool isLower,
        string? actualLevel = null)
    {
        var targetLevel = actualLevel ?? normalizedLevel;
        var secondarySkills = ParseJsonArray(objective.SecondarySkillsJson);
        var focusTags = ParseJsonArray(objective.FocusTagsJson);

        return new CurriculumRoutingRecommendation
        {
            TargetCefrLevel = targetLevel,
            AllowedCefrLevels = [targetLevel],
            PrimarySkill = objective.PrimarySkill,
            SecondarySkills = secondarySkills,
            CurriculumObjectiveKey = objective.Key,
            CurriculumObjectiveTitle = objective.Title,
            Subskill = objective.Subskill,
            ContextTags = contextTags,
            FocusTags = focusTags,
            DifficultyBand = difficultyBand,
            RoutingReason = reason,
            IsLowerLevelContent = isLower,
            Source = request.Source,
            Explanation = isLower
                ? $"Lower-level content ({targetLevel} vs student {normalizedLevel}): {reason}"
                : $"Exact-level match at {targetLevel}"
        };
    }

    private static CurriculumRoutingRecommendation BuildFallback(
        CurriculumRoutingRequest request,
        string normalizedLevel,
        IReadOnlyList<string> contextTags,
        int difficultyBand)
    {
        // Ensure fallback never defaults to workplace context.
        var safeContextTags = contextTags.Contains(CurriculumContextTagConstants.Workplace,
            StringComparer.OrdinalIgnoreCase)
            ? contextTags.Where(t => !t.Equals(CurriculumContextTagConstants.Workplace,
                StringComparison.OrdinalIgnoreCase)).ToList()
            : (IReadOnlyList<string>)contextTags;

        if (safeContextTags.Count == 0)
            safeContextTags = [CurriculumContextTagConstants.GeneralEnglish];

        return new CurriculumRoutingRecommendation
        {
            TargetCefrLevel = normalizedLevel,
            AllowedCefrLevels = [normalizedLevel],
            PrimarySkill = request.PrimarySkill,
            SecondarySkills = [],
            CurriculumObjectiveKey = null,
            CurriculumObjectiveTitle = null,
            ContextTags = safeContextTags,
            FocusTags = [],
            DifficultyBand = difficultyBand,
            RoutingReason = RoutingReason.Fallback,
            IsLowerLevelContent = false,
            Source = request.Source,
            Explanation = $"No curriculum objective found for level {normalizedLevel}; fallback to general_english"
        };
    }

    private static IReadOnlyList<string> ResolveFocusAreas(CurriculumRoutingRequest request)
    {
        var areas = request.FocusAreas
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .ToList();

        if (!string.IsNullOrWhiteSpace(request.CustomFocusArea))
            areas.Add(request.CustomFocusArea.Trim());

        return areas;
    }

    private static int ResolveDifficultyBand(string? difficultyPreference)
    {
        return difficultyPreference?.ToLowerInvariant() switch
        {
            "gentle" => 1,
            "challenging" => 4,
            _ => 2  // Balanced or null → band 2 (lower-mid, safe default)
        };
    }

    private static string? GetOneLevelDown(string cefrLevel)
    {
        var idx = Array.IndexOf(CoreCefrLevels, cefrLevel);
        return idx > 0 ? CoreCefrLevels[idx - 1] : null;
    }

    private static List<string> ParseJsonArray(string json)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(json) || json == "[]")
                return [];

            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray()
                .Where(e => e.ValueKind == System.Text.Json.JsonValueKind.String)
                .Select(e => e.GetString()!)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
